using System;
using System.Collections.Generic;
using System.Linq;

namespace ColonyCouncil
{
    /// <summary>
    /// The pages of the mod settings window, in the order of the list on its left: what a player thinks about, not how
    /// the mod is built (the review's decision 15, 6 November 2026; seven pages by system before).
    /// </summary>
    public enum SettingsPage
    {
        /// <summary>How much happens: every part's rate, the colony's pace first.</summary>
        HowMuch,
        /// <summary>How hard: the stakes, pressures and standards, strain, illness and hurts.</summary>
        HowHard,
        /// <summary>What's on: each part by what it is about (the council, lives, bonds, the story, the world, the peoples, the tools).</summary>
        WhatsOn,
        /// <summary>How you're told: messages and letters, speech on the map, alerts, buttons and windows, the panels, the bar.</summary>
        HowTold,
        /// <summary>Your other mods: only those loaded show.</summary>
        OtherMods
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
        /// <summary>The other mods it is for, by package id: it shows only while all of them are loaded (the other mods' page).</summary>
        public string[] mods;

        public bool IsField => !notAField;

        /// <summary>
        /// Drawn under the switch it hangs on, when that switch is on the same page (a rate or a knob on another page
        /// stands alone). Worked out once, when the catalog is built.
        /// </summary>
        public bool Indented { get; internal set; }

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
        /// <summary>The "what's on" setups at the top of the What's on page (as opposed to the left-hand style presets).</summary>
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
            ["era"] = "kept only so old settings files load; the era belongs to each colony now (the Era row at the foot of the What's on page)",
            ["savedRooms"] = "the rooms the player saved from a map, managed in the room planner",
            ["planMaterials"] = "the materials last picked in the room planner",
            ["lastCouncilPage"] = "the council window's page when it was last closed",
            ["roomsButton"] = "the old switch for the rooms button, read once into the planning button's (the row \"Show the planning button\") so older settings files load",
            ["planningMerged"] = "whether the rooms and planning buttons' settings were made one, so it is done once (BarMoreMath.MergePlanning)",
            ["ordinaryMerged"] = "whether the four rules that softened good moods were read into one setting, so it is done once (GoodLifeMath.MergedSetting)",
            ["lastAlmanacPage"] = "the Almanac's page when it was last closed",
            ["almanacFolded"] = "whether the Almanac's categories and page list are folded away, switched in the Almanac itself",
            ["moodDetails"] = "whether the Mood tab and the Moods page show every mood as a card, switched on those pages (Compact / Details)",
            ["hudVersion"] = "the on-screen panels' settings version, so a change to older settings is made once (HudLayout.Migrate)",
            ["hudPanels"] = "each on-screen panel's place and options, set on the panels themselves (their menu) or with the buttons under \"On-screen panels\"",
            ["councilPageState"] = "the filters and sort orders the council's pages remember, set on the pages themselves",
            ["lastSeenVersion"] = "the Rimfolk version the player last saw, so the letter after an update comes once (the row \"What's new letter\")",
            ["speechText"] = "the old switch for colonists speaking, kept in step with speechShows (the row \"Colonists' speech shows as\") so older settings files load",
            ["showImpact"] = "the old switch for what each answer does, kept in step with answerImpact (the row \"What each answer does\") so older settings files load",
            ["impactShows"] = "the choice for what each answer does before the flavour line came, kept in step with answerImpact (the row \"What each answer does\") so older settings files load",
            ["haulImported"] = "whether the standalone Harvest and Haul's settings file was read into the carrying and hauling rows, so it is done once (HaulLinks.ImportStandaloneSettings)"
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
            foreach (var e in all)
            {
                string parent = e.Parent;
                e.Indented = parent != null && Find(parent)?.page == e.page;
            }
            BuildPresets();
        }

        // ---------------- The pages ----------------

        private static SettingsPage curPage;
        private static string curSection;

        private static void Build()
        {
            // The five pages ask what a player thinks about (the review's decision 15, 6 November 2026): how much happens,
            // how hard, what's on, how you're told, your other mods. Each row keeps its field, so its saved key and value.

            // ================= How much happens: every part's rate in one place =================
            Page(SettingsPage.HowMuch);
            // The colony's pace (PlayOut/Stage.cs): its own doings come only while the week has room.
            Section("CC_SetSec_Pace");
            NoteUnder("CC_SetSec_Pace_Note");
            Slider("gatheringsAWeek", "CC_Settings_GatheringsAWeek", "CC_SetNote_GatheringsAWeek", StageMath.MinAWeek, StageMath.MaxAWeek, 1f, "0");
            Section("CC_SetSec_CouncilAsks");
            Slider("daysBetweenWishes", "CC_Settings_DaysBetweenWishes", "CC_SetNote_DaysBetweenWishes", 0.5f, 10f, 0.5f, "0.0", "enableWishes");
            Slider("maxOpenWishes", "CC_Settings_MaxOpenWishes", "CC_SetNote_MaxOpenWishes", 1f, 12f, 1f, "0", "enableWishes");
            Slider("daysBetweenDilemmas", "CC_Settings_DaysBetweenDilemmas", "CC_SetNote_DaysBetweenDilemmas", 3f, 30f, 1f, "0", "enableDilemmas");
            Slider("daysBetweenMeetings", "CC_Settings_DaysBetweenMeetings", "CC_SetNote_DaysBetweenMeetings", 5f, 60f, 1f, "0", "enableAmbitions");
            Section("CC_SetSec_Lives");
            Slider("momentFrequency", "CC_Settings_FreqMoments", "CC_SetNote_FreqMoments", 0.25f, 3f, 0.25f, "%");
            Slider("socialEventFrequency", "CC_Settings_FreqSocial", "CC_SetNote_FreqSocial", 0.25f, 3f, 0.25f, "%");
            Slider("dreamFrequency", "CC_Settings_FreqDreams", "CC_SetNote_FreqDreams", 0.25f, 3f, 0.25f, "%");
            Choice("arcFrequency", "CC_Settings_Arcs", "CC_Settings_Arcs_Tip", "CC_Arcs_", "CC_SetNote_Arcs_", new[] { "Off", "Rare", "Normal", "Frequent" });
            Slider("daysBetweenPersonalTouches", "CC_Settings_DaysBetweenTouches", "CC_SetNote_DaysBetweenTouches", 1f, 30f, 1f, "0", "enablePersonalTouches");
            // The animals' temperaments on the map (Animals/AnimalTempers.cs): the greedy one at the stores, the loyal one at the door.
            Slider("animalMoments", "CC_Settings_AnimalMoments", "CC_SetNote_AnimalMoments", 0f, 2f, 0.05f, "%", "petMoments,animalTempers");
            Slider("outsiderChatterFrequency", "CC_Settings_OutsiderChatterFrequency", "CC_SetNote_OutsiderChatterFrequency", 0.25f, 3f, 0.25f, "%",
                "outsiderTalk,outsiderChatter");
            Section("CC_SetSec_WorldPace");
            Slider("encounterFrequency", "CC_Settings_FreqEncounters", "CC_SetNote_FreqEncounters", 0.25f, 3f, 0.25f, "%");
            Slider("storytellerEventWeight", "CC_Settings_StorytellerEventWeight", "CC_SetNote_StorytellerEventWeight", 0.25f, 2f, 0.05f, "%",
                "worldEvents,eventsFromStoryteller");
            Slider("daysBetweenWorldEvents", "CC_Settings_DaysBetweenWorldEvents", "CC_SetNote_DaysBetweenWorldEvents", 1f, 20f, 0.5f, "0.#",
                "worldEvents,!eventsFromStoryteller", "CC_SetWhy_OwnTimer");
            Slider("daysBetweenWorldSituations", "CC_Settings_DaysBetweenWorldSituations", "CC_SetNote_DaysBetweenWorldSituations", 15f, 120f, 5f, "0",
                "worldSituations");
            // The region (World/Region/): how much a remote home draws travellers, and how likely raiders' camps are.
            Slider("remoteTravellers", "CC_Settings_RemoteTravellers", "CC_SetNote_RemoteTravellers", 0f, 2f, 0.05f, "%");
            Slider("raidersCamps", "CC_Settings_RaidersCamps", "CC_SetNote_RaidersCamps", 0f, 2f, 0.05f, "%");
            // Rimfolk's peoples (Peoples/Arising.cs): how often a new people arises from what happened in the region.
            Slider("newPeoples", "CC_Settings_NewPeoples", "CC_SetNote_NewPeoples", 0f, 2f, 0.05f, "%");
            // The wild's alphas and bosses (Beasts/): a pack's alpha, a beast nemesis's flight and return, a legend of the
            // region, the great herds and their stampedes, each its own rate.
            Slider("packAlphas", "CC_Settings_PackAlphas", "CC_SetNote_PackAlphas", 0f, 2f, 0.05f, "%");
            Slider("beastNemeses", "CC_Settings_BeastNemeses", "CC_SetNote_BeastNemeses", 0f, 2f, 0.05f, "%", "nemeses");
            Slider("regionLegends", "CC_Settings_RegionLegends", "CC_SetNote_RegionLegends", 0f, 2f, 0.05f, "%", "worldSituations");
            Slider("greatHerds", "CC_Settings_GreatHerds", "CC_SetNote_GreatHerds", 0f, 2f, 0.05f, "%");
            // The bonds with other peoples (Diplomacy/Schemes.cs): how often those with a grudge scheme against the colony.
            Slider("theirSchemes", "CC_Settings_TheirSchemes", "CC_SetNote_TheirSchemes", 0f, 2f, 0.05f, "%", "answersPlayOut");

            // ================= How hard: what's at stake, pressures, strain, illness and hurts =================
            Page(SettingsPage.HowHard);
            Section("CC_Settings_Stakes");
            Choice("stakes", "CC_Settings_Stakes", "CC_SetTip_Stakes", "CC_Stakes_", "CC_Stakes_Short_", new[] { "Gentle", "Balanced", "Demanding", "AdvisorOnly" });
            Slider("penaltyFactor", "CC_Settings_Penalty", "CC_SetNote_Penalty", 0.25f, 2f, 0.05f, "%", "stakes=Balanced|Demanding", "CC_SetWhy_Downsides");
            Slider("rewardFactor", "CC_Settings_Reward", "CC_SetNote_Reward", 0.25f, 2f, 0.05f, "%", "stakes!=AdvisorOnly", "CC_SetWhy_MoodEffects");
            Slider("wishDurationFactor", "CC_Settings_WishDuration", "CC_SetNote_WishDuration", 0.5f, 3f, 0.05f, "%", "enableWishes");
            Slider("ambitionDeadlineFactor", "CC_Settings_AmbitionDeadline", "CC_SetNote_AmbitionDeadline", 0.5f, 3f, 0.05f, "%", "enableAmbitions");
            Toggle("jealousy", "CC_Settings_Jealousy", "CC_SetNote_Jealousy", "stakes=Balanced|Demanding", "CC_SetWhy_Downsides");
            Toggle("wishesTakenBack", "CC_Settings_TakenBack", "CC_SetNote_TakenBack", "stakes=Balanced|Demanding", "CC_SetWhy_Downsides");
            Choice("takenBackGrace", "CC_Settings_KeptGrace", "CC_SetTip_KeptGrace", "CC_KeptGrace_", "CC_SetNote_KeptGrace_", new[] { "Off", "Short", "Normal", "Long" },
                "wishesTakenBack,stakes=Balanced|Demanding", "CC_SetWhy_Downsides");
            Toggle("allowQuitting", "CC_Settings_Quitting", "CC_SetNote_Quitting");
            Section("CC_SetSec_Moods");
            Choice("lifePressure", "CC_Settings_Life", "CC_Settings_Life_Tip", "CC_Life_", "CC_SetNote_Life_", new[] { "Gentle", "Normal", "Demanding" },
                "stakes!=AdvisorOnly", "CC_SetWhy_MoodEffects");
            Toggle("risingStandards", "CC_Settings_Standards", "CC_SetNote_Standards");
            Toggle("goodGrowsOrdinary", "CC_Settings_Ordinary", "CC_SetNote_Ordinary", "stakes!=AdvisorOnly", "CC_SetWhy_MoodEffects");
            Toggle("quietTroubles", "CC_Settings_QuietTroubles", "CC_SetNote_QuietTroubles", "stakes!=AdvisorOnly", "CC_SetWhy_MoodEffects");
            Toggle("frictions", "CC_Settings_Frictions", "CC_SetNote_Frictions");
            // The heart (Heart/): how fast strain builds, the day off at the end of one's rope, and the good side against it.
            Section("CC_Settings_HeartHeader");
            Slider("strainSpeed", "CC_Settings_StrainSpeed", "CC_SetNote_StrainSpeed", 0.5f, 1.5f, 0.05f, "%", "heart");
            Toggle("heartEpisodes", "CC_Settings_HeartEpisodes", "CC_SetNote_HeartEpisodes", "heart");
            // The heart's good side (Heart/GoodHeart.cs): in good heart, in high spirits.
            Toggle("heartGoodSide", "CC_Settings_HeartGoodSide", "CC_SetNote_HeartGoodSide", "heart");
            Section("CC_Settings_AilmentsHeader");
            Slider("ailmentFrequency", "CC_Settings_AilmentFrequency", "CC_SetNote_AilmentFrequency", 0.25f, 2f, 0.05f, "%", "ailments");
            Toggle("seriousIllnesses", "CC_Settings_SeriousIllnesses", "CC_SetNote_SeriousIllnesses", "ailments");
            // The shared illness budget (Ailments/IllnessBudget.cs): Rimfolk's serious illnesses take a share of the game's own rate.
            Toggle("illnessBudget", "CC_Settings_IllnessBudget", "CC_SetNote_IllnessBudget", "seriousIllnesses,deepHooks");
            Section("CC_SetSec_LifeRisks");
            Toggle("crowdedSickness", "CC_Settings_CrowdedSickness", "CC_SetNote_CrowdedSickness", "seriousIllnesses");
            Toggle("frailty", "CC_Settings_Frailty", "CC_SetNote_Frailty");
            Toggle("dirtyWounds", "CC_Settings_DirtyWounds", "CC_SetNote_DirtyWounds");
            Toggle("endOfLife", "CC_Settings_EndOfLife", "CC_SetNote_EndOfLife");
            // Hurts from everyday life (Harm/): one factor on every Rimfolk-made hurt, the harm review's shared rules; and fights.
            Section("CC_SetSec_Hurts");
            NoteUnder("CC_SetSec_Hurts_Note");
            Slider("everydayHurts", "CC_Settings_EverydayHurts", "CC_SetNote_EverydayHurts", 0f, 2f, 0.05f, "%");
            Slider("scenarioThreat", "CC_Settings_ScenarioThreat", "CC_SetNote_ScenarioThreat", 0.25f, 2f, 0.25f, "%");
            Toggle("worldEventBattles", "CC_Settings_WorldEventBattles", "CC_SetNote_WorldEventBattles", "worldEvents");

            // ================= What's on: each part, by what it is about =================
            Page(SettingsPage.WhatsOn);
            Section("CC_Settings_Presets");
            Action("quickSetup", "CC_Settings_Presets", "CC_Settings_Presets_Tip", "CC_SetNote_QuickSetup");
            // The council: the advisor and the plan, wishes, meetings, questions, decrees and offices.
            Section("CC_Settings_Layers");
            Toggle("enableAdvisor", "CC_Settings_EnableAdvisor", "CC_SetNote_EnableAdvisor");
            Toggle("nextSteps", "CC_Settings_NextSteps", "CC_SetNote_NextSteps");
            Toggle("enableWishes", "CC_Settings_EnableWishes", "CC_SetNote_EnableWishes");
            Toggle("eventWishes", "CC_Settings_EventWishes", "CC_SetNote_EventWishes", "enableWishes");
            Toggle("reminders", "CC_Settings_Reminders", "CC_SetNote_Reminders", "enableWishes");
            Toggle("selfHelp", "CC_Settings_SelfHelp", "CC_SetNote_SelfHelp", "enableWishes");
            Toggle("wishesForOthers", "CC_Settings_ForOthers", "CC_SetNote_ForOthers", "enableWishes");
            Toggle("petitions", "CC_Settings_Petitions", "CC_SetNote_Petitions", "enableWishes");
            Toggle("enableAmbitions", "CC_Settings_EnableAmbitions", "CC_SetNote_EnableAmbitions");
            Toggle("meetingsCallFromWork", "CC_Settings_MeetingsCallFromWork", "CC_SetNote_MeetingsCallFromWork", "enableAmbitions");
            Toggle("enableDilemmas", "CC_Settings_Dilemmas", "CC_SetNote_Dilemmas");
            Toggle("councilBanishment", "CC_Settings_Banishment", "CC_SetNote_Banishment", "enableDilemmas");
            Toggle("movingOn", "CC_Settings_MovingOn", "CC_SetNote_MovingOn");
            Toggle("decreeRequests", "CC_Settings_DecreeRequests", "CC_SetNote_DecreeRequests");
            Toggle("offices", "CC_Settings_Offices", "CC_SetNote_Offices");
            Toggle("electionEvent", "CC_Settings_ElectionEvent", "CC_SetNote_ElectionEvent", "offices");
            Toggle("colonyCharacter", "CC_Settings_Character", "CC_SetNote_Character");
            Toggle("interplay", "CC_Settings_Interplay", "CC_SetNote_Interplay");
            // People's lives: the heart, life goals, quirks, dreams, moments, health.
            Section("CC_SetSec_Lives");
            Toggle("heart", "CC_Settings_Heart", "CC_SetNote_Heart");
            Toggle("aspirations", "CC_Settings_Aspirations", "CC_SetNote_Aspirations");
            Toggle("quirks", "CC_Settings_Quirks", "CC_SetNote_Quirks");
            Toggle("heirlooms", "CC_Settings_Heirlooms", "CC_SetNote_Heirlooms");
            Toggle("comingOfAge", "CC_Settings_ComingOfAge", "CC_SetNote_ComingOfAge");
            Toggle("autonomy", "CC_Settings_Autonomy", "CC_SetNote_Autonomy");
            Toggle("autonomyPlans", "CC_Settings_AutonomyPlans", "CC_SetNote_AutonomyPlans", "autonomy");
            Toggle("dreams", "CC_Settings_Dreams", "CC_SetNote_Dreams");
            Toggle("dreamOmens", "CC_Settings_DreamOmens", "CC_SetNote_DreamOmens", "dreams");
            Toggle("colonistMoments", "CC_Settings_Moments", "CC_SetNote_Moments");
            Toggle("worldReactions", "CC_Settings_WorldReactions", "CC_SetNote_WorldReactions");
            Toggle("smallHabits", "CC_Settings_SmallHabits", "CC_SetNote_SmallHabits");
            Toggle("ailments", "CC_Settings_Ailments", "CC_SetNote_Ailments");
            Toggle("convalescence", "CC_Settings_Convalescence", "CC_SetNote_Convalescence");
            Toggle("healerReputations", "CC_Settings_HealerReputations", "CC_SetNote_HealerReputations");
            Toggle("medicineByNeed", "CC_Settings_MedicineByNeed", "CC_SetNote_MedicineByNeed", "deepHooks");
            Choice("harmlessMedicine", "CC_Settings_HarmlessMedicine", "CC_Settings_HarmlessMedicine_Tip", "CC_HarmlessMed_", "CC_SetNote_HarmlessMed_",
                new[] { "None", "Herbal" }, "medicineByNeed");
            Toggle("restByNeed", "CC_Settings_RestByNeed", "CC_SetNote_RestByNeed", "deepHooks");
            Section("CC_Settings_Bonds");
            Toggle("socialWeb", "CC_Settings_SocialWeb", "CC_SetNote_SocialWeb");
            Toggle("socialEvents", "CC_Settings_SocialEvents", "CC_SetNote_SocialEvents", "socialWeb");
            Toggle("bondsTakeTime", "CC_Settings_BondsTakeTime", "CC_SetNote_BondsTakeTime", "socialWeb");
            Toggle("chemistryLimits", "CC_Settings_ChemistryLimits", "CC_SetNote_ChemistryLimits", "socialWeb");
            Toggle("comradesByDanger", "CC_Settings_ComradesByDanger", "CC_SetNote_ComradesByDanger", "socialWeb");
            Toggle("deedsRemembered", "CC_Settings_DeedsRemembered", "CC_SetNote_DeedsRemembered", "socialWeb");
            Toggle("seatFeelings", "CC_Settings_SeatFeelings", "CC_SetNote_SeatFeelings", "socialWeb,offices");
            Toggle("loveFeelings", "CC_Settings_LoveFeelings", "CC_SetNote_LoveFeelings", "socialWeb");
            Toggle("socialLife", "CC_Settings_SocialLife", "CC_SetNote_SocialLife");
            Toggle("traitChanges", "CC_Settings_TraitChanges", "CC_SetNote_TraitChanges", "socialLife");
            Toggle("faithLife", "CC_Settings_FaithLife", "CC_SetNote_FaithLife");
            Toggle("secrets", "CC_Settings_Secrets", "CC_SetNote_Secrets");
            Toggle("vendettas", "CC_Settings_Vendettas", "CC_SetNote_Vendettas");
            // The colony's story: journals, names, the year, the calendar, and answers and goods on the map.
            Section("CC_SetSec_Story");
            Toggle("journals", "CC_Settings_Journals", "CC_SetNote_Journals");
            Toggle("epithets", "CC_Settings_Epithets", "CC_SetNote_Epithets", tip: "CC_Name_Settings_Epithets_Tip");
            Toggle("anniversaries", "CC_Settings_Anniversaries", "CC_SetNote_Anniversaries");
            Toggle("annals", "CC_Settings_Annals", "CC_SetNote_Annals");
            Toggle("eulogies", "CC_Settings_Eulogies", "CC_SetNote_Eulogies");
            Toggle("colonyCalendar", "CC_Settings_Calendar", "CC_SetNote_Calendar");
            Toggle("answersPlayOut", "CC_Settings_AnswersPlayOut", "CC_SetNote_AnswersPlayOut");
            Toggle("goodsComeAndGo", "CC_Settings_GoodsComeAndGo", "CC_SetNote_GoodsComeAndGo");
            Section("CC_Settings_Strangers");
            Toggle("strangers", "CC_Settings_Encounters", "CC_SetNote_Encounters");
            Toggle("nemeses", "CC_Settings_Nemeses", "CC_SetNote_Nemeses");
            Toggle("nemesisReturns", "CC_Settings_NemesisReturns", "CC_SetNote_NemesisReturns", "nemeses");
            Toggle("petMoments", "CC_Settings_PetMoments", "CC_SetNote_PetMoments");
            Toggle("sacredAnimals", "CC_Settings_SacredAnimals", "CC_SetNote_SacredAnimals");
            Toggle("omenAnimals", "CC_Settings_OmenAnimals", "CC_SetNote_OmenAnimals");
            // The animals' natures (Animals/): temperaments, friendships and grief among them, and (a fantasy part) speech.
            Toggle("animalTempers", "CC_Settings_AnimalTempers", "CC_SetNote_AnimalTempers", "petMoments");
            Toggle("animalBonds", "CC_Settings_AnimalBonds", "CC_SetNote_AnimalBonds", "petMoments");
            Choice("speakingAnimals", "CC_Settings_SpeakingAnimals", "CC_Settings_SpeakingAnimals_Tip", "CC_SpeakingAnimals_", "CC_SetNote_SpeakingAnimals_",
                new[] { "Auto", "On", "Off" }, "petMoments");
            // The colony's animals hunting for themselves (Animals/PetHunts.cs): not a part of the companions' life, so on its own.
            Toggle("petsHunt", "CC_Settings_PetsHunt", "CC_SetNote_PetsHunt");
            // A bleeding animal's body taken when it dies (Animals/BodyWhenItDies.cs): the player's own order, with the kill's hook.
            Toggle("bodyWhenItDies", "CC_Settings_BodyWhenItDies", "CC_SetNote_BodyWhenItDies", "deepHooks");
            Section("CC_Settings_WorldEventsHeader");
            Toggle("worldEvents", "CC_Settings_WorldEvents", "CC_SetNote_WorldEvents");
            Toggle("camps", "CC_Settings_Camps", "CC_SetNote_Camps", "worldEvents");
            // How many camps (Camps/CampCrowdMath.cs): the player's request of 8 November 2026, for a big map.
            Choice("campCrowd", "CC_Settings_CampCrowd", "CC_Settings_CampCrowd_Tip", "CC_CampCrowd_", "CC_SetNote_CampCrowd_", new[] { "Rare", "Usual", "Many", "Crowded" }, "camps");
            Toggle("missionsAsQuests", "CC_Settings_MissionsAsQuests", "CC_SetNote_MissionsAsQuests", "worldEvents");
            Toggle("eventsFromStoryteller", "CC_Settings_EventsFromStoryteller", "CC_SetNote_EventsFromStoryteller", "worldEvents");
            Section("CC_Settings_WorldSituationsHeader");
            Toggle("worldSituations", "CC_Settings_WorldSituations", "CC_SetNote_WorldSituations");
            Toggle("factionMemory", "CC_Settings_FactionMemory", "CC_SetNote_FactionMemory");
            // The region (World/Region/): the towns within the region map's reach kept in detail.
            Section("CC_SetSec_Region");
            Toggle("regionFortunes", "CC_Settings_RegionFortunes", "CC_SetNote_RegionFortunes");
            Toggle("regionStock", "CC_Settings_RegionStock", "CC_SetNote_RegionStock", "regionFortunes,deepHooks");
            Toggle("regionFaces", "CC_Settings_RegionFaces", "CC_SetNote_RegionFaces");
            Toggle("regionChange", "CC_Settings_RegionChange", "CC_SetNote_RegionChange");
            Toggle("tradingPosts", "CC_Settings_TradingPosts", "CC_SetNote_TradingPosts");
            Toggle("hamlets", "CC_Settings_Hamlets", "CC_SetNote_Hamlets");
            // Rimfolk's peoples rise and fall back an age over the years (Peoples/PeopleAges.cs).
            Toggle("peopleAges", "CC_Settings_PeopleAges", "CC_SetNote_PeopleAges");
            Toggle("smallTownMaps", "CC_Settings_SmallTownMaps", "CC_SetNote_SmallTownMaps", "deepHooks");
            // The colony's bonds with other peoples (Diplomacy/): one place for each kind of bond. Fealty and vassals are
            // pacts of their own kind (decision 14), so their switch hangs under the pacts': off with them.
            Section("CC_SetSec_Peoples");
            Toggle("diplomacyFavours", "CC_Settings_DiplomacyFavours", "CC_SetNote_DiplomacyFavours", "factionMemory");
            Toggle("diplomacyPacts", "CC_Settings_DiplomacyPacts", "CC_SetNote_DiplomacyPacts", "factionMemory");
            Toggle("diplomacyFealty", "CC_Settings_PactFealty", "CC_SetNote_PactFealty", "diplomacyPacts,factionMemory", tip: "CC_Settings_DiplomacyFealty_Tip");
            Toggle("truceRaids", "CC_Settings_TruceRaids", "CC_SetNote_TruceRaids", "diplomacyPacts,deepHooks");
            Toggle("diplomacyRansom", "CC_Settings_DiplomacyRansom", "CC_SetNote_DiplomacyRansom", "factionMemory");
            Toggle("diplomacyEnvoys", "CC_Settings_DiplomacyEnvoys", "CC_SetNote_DiplomacyEnvoys", "factionMemory");
            Toggle("diplomacyFeasts", "CC_Settings_DiplomacyFeasts", "CC_SetNote_DiplomacyFeasts", "factionMemory");
            Toggle("diplomacyMarriage", "CC_Settings_DiplomacyMarriage", "CC_SetNote_DiplomacyMarriage", "factionMemory");
            Toggle("diplomacyWards", "CC_Settings_DiplomacyWards", "CC_SetNote_DiplomacyWards", "factionMemory");
            Toggle("diplomacyRenown", "CC_Settings_DiplomacyRenown", "CC_SetNote_DiplomacyRenown", "factionMemory");
            Toggle("diplomacyNature", "CC_Settings_DiplomacyNature", "CC_SetNote_DiplomacyNature", "heart");
            Section("CC_SetSec_Neighbours");
            Toggle("notables", "CC_Settings_Notables", "CC_SetNote_Notables");
            Toggle("neighbourLetters", "CC_Settings_NeighbourLetters", "CC_SetNote_NeighbourLetters", "notables");
            // The post (Post/): the mailbag at the gate, carriers going that way, the regular post by the age, the range until radio.
            Toggle("post", "CC_Settings_Post", "CC_SetNote_Post", "neighbourLetters");
            Choice("postPace", "CC_Settings_PostPace", "CC_Settings_PostPace_Tip", "CC_PostPace_", "CC_SetNote_PostPace_", new[] { "Seldom", "Usual", "Often" }, "post");
            // Letters by occasion (World/LetterOccasions.cs): the kind letters only when there is a reason to write them.
            Toggle("letterOccasions", "CC_Settings_LetterOccasions", "CC_SetNote_LetterOccasions", "neighbourLetters");
            // Dealings with a whole people or town, pen friends, the world writing first (Dealings/).
            Toggle("townDealings", "CC_Settings_TownDealings", "CC_SetNote_TownDealings", "neighbourLetters");
            Toggle("penFriends", "CC_Settings_PenFriends", "CC_SetNote_PenFriends", "neighbourLetters");
            Toggle("worldWrites", "CC_Settings_WorldWrites", "CC_SetNote_WorldWrites", "neighbourLetters");
            Toggle("outsiderTalk", "CC_Settings_OutsiderTalk", "CC_SetNote_OutsiderTalk");
            Toggle("outsiderSpar", "CC_Settings_OutsiderSpar", "CC_SetNote_OutsiderSpar", "outsiderTalk");
            Toggle("outsiderProvoke", "CC_Settings_OutsiderProvoke", "CC_SetNote_OutsiderProvoke", "outsiderTalk,deepHooks");
            Toggle("outsiderDuels", "CC_Settings_OutsiderDuels", "CC_SetNote_OutsiderDuels", "outsiderTalk,deepHooks");
            Toggle("outsiderChatter", "CC_Settings_OutsiderChatter", "CC_SetNote_OutsiderChatter", "outsiderTalk");
            // The right-click's things done by the colonists themselves at a good moment (Outsiders/OutsiderAuto.cs).
            Choice("outsiderAuto", "CC_Settings_OutsiderAuto", "CC_Settings_OutsiderAuto_Tip", "CC_OutsiderAuto_", "CC_SetNote_OutsiderAuto_",
                new[] { "Off", "FreeOnly", "WithinLimits" }, "outsiderTalk");
            Section("CC_SetSec_Journeys");
            Toggle("journeys", "CC_Settings_Journeys", "CC_SetNote_Journeys");
            Toggle("journeyShiftSleep", "CC_Settings_JourneySleep", "CC_SetNote_JourneySleep", "journeys");
            Toggle("journeyClothes", "CC_Settings_JourneyClothes", "CC_SetNote_JourneyClothes", "journeys");
            Slider("journeyFreeHours", "CC_Settings_JourneyFreeHours", "CC_SetNote_JourneyFreeHours", 2f, 8f, 1f, "0", "journeys");
            // Many colonies (Colonies/): a fresh start in the same world, from a colony's card, and a colony handed over.
            Section("CC_SetSec_Colonies");
            Toggle("freshStarts", "CC_Settings_FreshStarts", "CC_SetNote_FreshStarts");
            Toggle("newcomers", "CC_Settings_Newcomers", "CC_SetNote_Newcomers", "freshStarts");
            Toggle("handOvers", "CC_Settings_HandOvers", "CC_SetNote_HandOvers");
            Toggle("townLife", "CC_Settings_TownLife", "CC_SetNote_TownLife");
            Toggle("manyMerge", "CC_Settings_ManyMerge", "CC_SetNote_ManyMerge");
            Section("CC_SetSec_Weather");
            Toggle("dynamicWeather", "CC_Settings_DynamicWeather", "CC_SetNote_DynamicWeather", "deepHooks");
            Toggle("weatherTemperature", "CC_Settings_WeatherTemperature", "CC_SetNote_WeatherTemperature", "deepHooks");
            Toggle("weatherForecast", "CC_Settings_WeatherForecast", "CC_SetNote_WeatherForecast", "deepHooks");
            Toggle("rainWashesMore", "CC_Settings_RainWash", "CC_SetNote_RainWash");
            // Building: personal touches, the build menu, the stores, defense.
            Section("CC_Settings_Touches");
            Toggle("enablePersonalTouches", "CC_Settings_EnableTouches", "CC_SetNote_EnableTouches");
            Section("CC_SetSec_BuildMenu");
            Toggle("buildMenuCounts", "CC_Settings_BuildMenuCounts", "CC_SetNote_BuildMenuCounts", "deepHooks");
            Toggle("buildPriorities", "CC_Settings_BuildPriorities", "CC_SetNote_BuildPriorities", "deepHooks");
            Section("CC_SetSec_Stores");
            Toggle("shelfReserves", "CC_Settings_ShelfReserves", "CC_SetNote_ShelfReserves", "deepHooks");
            Toggle("chewOutOfStores", "CC_Settings_ChewOutOfStores", "CC_SetNote_ChewOutOfStores", "deepHooks");
            // Carrying and hauling (Hauling/): the player's Harvest and Haul folded in, the long material run, things on the way.
            Section(HaulingSection);
            NoteUnder("CC_SetSec_Hauling_Note");
            Toggle("haulOwnWork", "CC_Settings_HaulOwnWork", "CC_SetNote_HaulOwnWork", "deepHooks");
            Toggle("haulPlants", "CC_Settings_HaulPlants", "CC_SetNote_HaulPlants", "haulOwnWork,deepHooks");
            Toggle("haulMining", "CC_Settings_HaulMining", "CC_SetNote_HaulMining", "haulOwnWork,deepHooks");
            Toggle("haulDeepDrill", "CC_Settings_HaulDeepDrill", "CC_SetNote_HaulDeepDrill", "haulOwnWork,deepHooks");
            Toggle("haulAnimals", "CC_Settings_HaulAnimals", "CC_SetNote_HaulAnimals", "haulOwnWork,deepHooks");
            Toggle("haulDeconstruct", "CC_Settings_HaulDeconstruct", "CC_SetNote_HaulDeconstruct", "haulOwnWork,deepHooks");
            Toggle("haulLeftoversNow", "CC_Settings_HaulLeftoversNow", "CC_SetNote_HaulLeftoversNow", "haulOwnWork,deepHooks");
            Toggle("haulFishing", "CC_Settings_HaulFishing", "CC_SetNote_HaulFishing", "haulOwnWork,deepHooks");
            Toggle("haulBills", "CC_Settings_HaulBills", "CC_SetNote_HaulBills", "haulOwnWork,deepHooks");
            Toggle("haulBuildings", "CC_Settings_HaulBuildings", "CC_SetNote_HaulBuildings", "haulOwnWork,deepHooks");
            Toggle("haulOtherWork", "CC_Settings_HaulOtherWork", "CC_SetNote_HaulOtherWork", "haulOwnWork,deepHooks");
            Toggle("haulChunks", "CC_Settings_HaulChunks", "CC_SetNote_HaulChunks", "haulOwnWork,deepHooks");
            Slider("haulMinTrip", "CC_Settings_HaulMinTrip", "CC_SetNote_HaulMinTrip", 0f, 30f, 0.5f, "0.#", "haulOwnWork,deepHooks");
            Slider("haulMaxHours", "CC_Settings_HaulMaxHours", "CC_SetNote_HaulMaxHours", 1f, 24f, 1f, "0", "haulOwnWork,deepHooks");
            Toggle("haulBeforeNeeds", "CC_Settings_HaulBeforeNeeds", "CC_SetNote_HaulBeforeNeeds", "haulOwnWork,deepHooks");
            Toggle("materialRun", "CC_Settings_MaterialRun", "CC_SetNote_MaterialRun", "deepHooks");
            Slider("materialRunDistance", "CC_Settings_MaterialRunDistance", "CC_SetNote_MaterialRunDistance", HaulMath.RunDistanceMin, HaulMath.RunDistanceMax, 5f, "0",
                "materialRun,deepHooks");
            Toggle("haulOnTheWay", "CC_Settings_HaulOnTheWay", "CC_SetNote_HaulOnTheWay", "deepHooks");
            Slider("haulFill", "CC_Settings_HaulFill", "CC_SetNote_HaulFill", 0.1f, 1f, 0.05f, "%", "deepHooks");
            Toggle("haulMechs", "CC_Settings_HaulMechs", "CC_SetNote_HaulMechs", "deepHooks");
            Toggle("haulLog", "CC_Settings_HaulLog", "CC_SetNote_HaulLog");
            Section("CC_SetSec_Defense");
            Toggle("coverDrag", "CC_Settings_CoverDrag", "CC_SetNote_CoverDrag", "deepHooks");
            Section("CC_SetSec_Planner");
            NoteUnder("CC_SetSec_Planner_Note");
            Toggle("planFurniture", "CC_SetLabel_PlanFurniture", "CC_SetNote_PlanFurniture", tip: "CC_SetTip_PlanFurniture");
            Toggle("planCutPlants", "CC_Planner_CutPlants", "CC_SetNote_PlanCutPlants");
            Toggle("planMineRock", "CC_Planner_MineRock", "CC_SetNote_PlanMineRock");
            Toggle("planReplaceOwn", "CC_Planner_ReplaceOwn", "CC_SetNote_PlanReplaceOwn");
            Toggle("planSmoothStone", "CC_Planner_Smooth", "CC_SetNote_PlanSmooth");
            // The council's automations (Building/Automations.cs): what it would only suggest, done for the player, each its own switch, all off to begin with.
            Section("CC_SetSec_Automations");
            NoteUnder("CC_SetSec_Automations_Note");
            Toggle("autoBills", "CC_Settings_AutoBills", "CC_SetNote_AutoBills");
            Toggle("autoBillMeals", "CC_Settings_AutoBillMeals", "CC_SetNote_AutoBillMeals", "autoBills");
            Toggle("autoBillMedicine", "CC_Settings_AutoBillMedicine", "CC_SetNote_AutoBillMedicine", "autoBills");
            Toggle("autoBillComponents", "CC_Settings_AutoBillComponents", "CC_SetNote_AutoBillComponents", "autoBills");
            Toggle("autoBillChains", "CC_Settings_AutoBillChains", "CC_SetNote_AutoBillChains", "autoBills");
            Toggle("autoPlan", "CC_Settings_AutoPlan", "CC_SetNote_AutoPlan", "nextSteps");
            Toggle("autoResearch", "CC_Settings_AutoResearch", "CC_SetNote_AutoResearch");
            Toggle("autoStockpile", "CC_Settings_AutoStockpile", "CC_SetNote_AutoStockpile");
            Toggle("autoTakeDown", "CC_Settings_AutoTakeDown", "CC_SetNote_AutoTakeDown");
            Toggle("autoLamps", "CC_Settings_AutoLamps", "CC_SetNote_AutoLamps", "enableAdvisor");
            Toggle("autoFirewood", "CC_Settings_AutoFirewood", "CC_SetNote_AutoFirewood", "enableAdvisor");
            Toggle("autoGrow", "CC_Settings_AutoGrow", "CC_SetNote_AutoGrow");
            Slider("autoBudget", "CC_Settings_AutoBudget", "CC_SetNote_AutoBudget", 50f, 1000f, 50f, "0");
            Slider("autoTakeDownsADay", "CC_Settings_AutoTakeDownsADay", "CC_SetNote_AutoTakeDownsADay", 1f, 30f, 1f, "0");
            Slider("autoChopsADay", "CC_Settings_AutoChopsADay", "CC_SetNote_AutoChopsADay", 1f, 30f, 1f, "0");
            // Planning ahead (Building/Ahead.cs): what the stock targets on the Making page are worked out from.
            Section("CC_SetSec_Ahead");
            NoteUnder("CC_SetSec_Ahead_Note");
            Slider("aheadMealDays", "CC_Settings_AheadMealDays", "CC_SetNote_AheadMealDays", 0.5f, 6f, 0.5f, "0.#");
            Slider("aheadReserveDays", "CC_Settings_AheadReserveDays", "CC_SetNote_AheadReserveDays", 1f, 30f, 1f, "0");
            Toggle("fieldsByWeather", "CC_Settings_FieldsByWeather", "CC_SetNote_FieldsByWeather", "deepHooks");
            Toggle("frostHarvestSuggest", "CC_Settings_FrostHarvest", "CC_SetNote_FrostHarvest");
            Toggle("frostHarvestAuto", "CC_Settings_FrostHarvestAuto", "CC_SetNote_FrostHarvestAuto", "frostHarvestSuggest");
            // The trade screen (Trade/): Rimfolk's own trade window, sorts by price, the marks and the basket from what the
            // colony needs, the readouts, the market card and window, and the colony's own trade record.
            Section("CC_SetSec_TradeScreen");
            NoteUnder("CC_SetSec_TradeScreen_Note");
            Toggle("tradeWindow", "CC_Settings_TradeWindow", "CC_SetNote_TradeWindow", "deepHooks");
            Toggle("tradeSorts", "CC_Settings_TradeSorts", "CC_SetNote_TradeSorts");
            Toggle("tradeMarks", "CC_Settings_TradeMarks", "CC_SetNote_TradeMarks", "deepHooks");
            Toggle("tradeBasket", "CC_Settings_TradeBasket", "CC_SetNote_TradeBasket", "deepHooks");
            Toggle("tradeReadouts", "CC_Settings_TradeReadouts", "CC_SetNote_TradeReadouts", "deepHooks");
            Toggle("tradeMarket", "CC_Settings_TradeMarket", "CC_SetNote_TradeMarket", "deepHooks");
            Toggle("tradeRecord", "CC_Settings_TradeRecord", "CC_SetNote_TradeRecord", "deepHooks");
            // The deeper game hooks, this colony's era, a check of what's working and a full reset.
            Section("CC_SetSec_Hooks");
            Toggle("deepHooks", "CC_Settings_DeepHooks", "CC_SetNote_DeepHooks");
            Section("CC_Settings_Era");
            var era = Choice("colonyEra", "CC_SetLabel_Era", "CC_SetTip_Era", "CC_Era_", "CC_SetNote_Era_", new[] { "Auto", "PreIndustrial", "Industrial" });
            era.notAField = true;
            era.whyOff = "CC_Settings_EraNoGame";
            Section("CC_SetSec_Checks");
            Action("selfCheck", "CC_SetLabel_SelfCheck", "CC_SetTip_SelfCheck", "CC_SetNote_SelfCheck").whyOff = "CC_SetWhy_NoGame";
            Action("resetAll", "CC_Settings_Reset", "CC_SetTip_Reset", "CC_SetNote_Reset");

            // ================= How you're told: messages, speech, alerts, buttons, windows, panels, the bar =================
            Page(SettingsPage.HowTold);
            Section("CC_Settings_Messages");
            Choice("messageLevel", "CC_SetLabel_MessageLevel", "CC_SetTip_MessageLevel", "CC_MsgLevel_", "CC_SetNote_MsgLevel_", new[] { "Everything", "Notable", "LettersOnly" });
            Choice("colonyNews", "CC_SetLabel_ColonyNews", "CC_SetTip_ColonyNews", "CC_NewsMode_", "CC_SetNote_News_", new[] { "AsItHappens", "Digest", "Off" });
            Slider("messagesADay", "CC_Settings_TellMessagesADay", "CC_SetNote_TellMessagesADay", TellMath.MinMessagesADay, TellMath.MaxMessagesADay, 1f, "0");
            Slider("lettersADay", "CC_Settings_TellLettersADay", "CC_SetNote_TellLettersADay", TellMath.MinLettersADay, TellMath.MaxLettersADay, 1f, "0");
            Toggle("allDreamMessages", "CC_Settings_AllDreamMessages", "CC_SetNote_AllDreamMessages", "dreams");
            Toggle("wishLetters", "CC_Settings_WishLetters", "CC_SetNote_WishLetters");
            Toggle("grantedLetters", "CC_Settings_GrantedLetters", "CC_SetNote_GrantedLetters");
            Toggle("whatsNewLetter", "CC_Settings_WhatsNewLetter", "CC_SetNote_WhatsNewLetter");
            Toggle("letterNotes", "CC_Settings_LetterNotes", "CC_SetNote_LetterNotes", "deepHooks");
            Choice("answerImpact", "CC_Settings_ShowImpact", "CC_Settings_ShowImpact_Tip", "CC_ImpactShows_", "CC_SetNote_ImpactShows_", new[] { "Flavour", "Hints", "Exact", "Off" });
            // What shows on the map and in the game's own tabs.
            Section("CC_SetSec_OnTheMap");
            Choice("speechShows", "CC_SetLabel_SpeechShows", "CC_SetTip_SpeechShows", "CC_SpeechShows_", "CC_SetNote_SpeechShows_",
                new[] { "Bubbles", "Floating", "Off" });
            Toggle("visualTouches", "CC_Settings_VisualTouches", "CC_SetNote_VisualTouches");
            Toggle("moodBubbleText", "CC_Settings_MoodBubbleText", "CC_SetNote_MoodBubbleText");
            Toggle("moodInsight", "CC_Settings_MoodInsight", "CC_SetNote_MoodInsight");
            Toggle("needsTabHints", "CC_Settings_NeedsTabHints", "CC_SetNote_NeedsTabHints", "moodInsight");
            // Which things have the wrong style for a colonist's ideoligion (Moods/StyleSurroundings.cs, with Ideology).
            Toggle("styleNotes", "CC_Settings_StyleNotes", "CC_SetNote_StyleNotes", "deepHooks");
            Toggle("heartInspector", "CC_Settings_HeartInspector", "CC_SetNote_HeartInspector", "heart");
            Toggle("socialTabColumn", "CC_Settings_SocialTabColumn", "CC_SetNote_SocialTabColumn", "deepHooks");
            Section("CC_Settings_Alerts");
            Toggle("advisorAlert", "CC_Settings_AdvisorAlert", "CC_SetNote_AdvisorAlert", "enableAdvisor");
            Toggle("wishAlert", "CC_Settings_WishAlert", "CC_SetNote_WishAlert", "enableWishes");
            Toggle("defenseGateReminder", "CC_Settings_DefenseGateReminder", "CC_SetNote_DefenseGateReminder");
            Toggle("freshCarcassAlert", "CC_Settings_FreshCarcassAlert", "CC_SetNote_FreshCarcassAlert");
            Toggle("noFoodAlert", "CC_Settings_NoFoodAlert", "CC_SetNote_NoFoodAlert");
            // Rimfolk's buttons and windows.
            Section("CC_SetSec_Buttons");
            Toggle("slowButtons", "CC_Settings_SlowButtons", "CC_SetNote_SlowButtons");
            var reset = Action("slowButtonsReset", "CC_Settings_SlowButtonsReset", null, "CC_SetNote_SlowButtonsReset", "slowButtons");
            reset.covers = new[] { "slowButtonsX", "slowButtonsY" };
            Dropdown("buttonPosition", "CC_Settings_ButtonPosition", "CC_SetTip_ButtonPosition", "CC_SetNote_ButtonPosition", "CC_ButtonPos_",
                new[] { "BeforeMenu", "AfterResearch", "AfterHistory", "AfterWork" });
            Toggle("worldButton", "CC_Settings_WorldButton", "CC_SetNote_WorldButton");
            Toggle("defenseButton", "CC_Settings_DefenseButton", "CC_SetNote_DefenseButton");
            Toggle("planButton", "CC_Settings_PlanButton", "CC_SetNote_PlanButton");
            Toggle("inboxCountOnButton", "CC_Settings_InboxCount", "CC_SetNote_InboxCount");
            Toggle("barMore", "CC_Settings_BarMore", "CC_SetNote_BarMore", "deepHooks");
            var arrange = Action("barArrange", "CC_Settings_BarArrange", "CC_Settings_BarArrange_Tip", "CC_SetNote_BarArrange", "barMore,deepHooks");
            arrange.covers = new[] { "barPutAway" };
            Choice("councilOpensOn", "CC_SetLabel_CouncilOpensOn", "CC_SetTip_CouncilOpensOn", "CC_CouncilOpens_", "CC_SetNote_CouncilOpens_",
                new[] { "ThisWeek", "LastPage" });
            Choice("almanacTextSize", "CC_SetLabel_AlmanacTextSize", "CC_SetTip_AlmanacTextSize", "CC_TextSize_", "CC_SetNote_TextSize_",
                new[] { "Small", "Normal", "Large", "Larger" });
            // How large Rimfolk's four windows are (UI/WindowLayoutMath.cs): standard, or grown on a large screen.
            Choice("windowSize", "CC_SetLabel_WindowSize", "CC_SetTip_WindowSize", "CC_WindowSize_", "CC_SetNote_WindowSize_", new[] { "Standard", "Large" });
            // Rimfolk's own look and its windows that move and size (docs/design/rimfolk-look.md, UI/WindowFrame.cs).
            Toggle("ownLook", "CC_Settings_OwnLook", "CC_SetNote_OwnLook");
            Toggle("windowsMove", "CC_Settings_WindowsMove", "CC_SetNote_WindowsMove");
            var places = Action("windowsPlaceReset", "CC_Settings_WindowsPlaceReset", null, "CC_SetNote_WindowsPlaceReset", "windowsMove");
            places.covers = new[] { "windowPlaces" };
            Slider("regionMapRadius", "CC_Settings_RegionMapRadius", "CC_SetNote_RegionMapRadius", 20f, 60f, 1f, "0");
            // Names that lead to their cards in Rimfolk's windows, and their previews (UI/Cards/).
            Section("CC_SetSec_Links");
            Toggle("linkPreviews", "CC_Settings_LinkPreviews", "CC_SetNote_LinkPreviews");
            Slider("linkPreviewDelay", "CC_Settings_LinkPreviewDelay", "CC_SetNote_LinkPreviewDelay", 0.1f, 1.5f, 0.05f, "0.00", "linkPreviews");
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

            // ================= Your other mods: only those loaded show (the page's foot names the rest) =================
            Page(SettingsPage.OtherMods);
            Section("CC_SetSec_Mod_Hospitality");
            Mod(Toggle("guestWill", "CC_Settings_GuestWill", "CC_SetNote_GuestWill", "deepHooks"), HospitalityId);
            Mod(Toggle("guestStories", "CC_Settings_GuestStories", "CC_SetNote_GuestStories"), HospitalityId);
            Mod(Toggle("guestCheck", "CC_Settings_GuestCheck", "CC_SetNote_GuestCheck", "deepHooks"), HospitalityId);
            Mod(Toggle("guestGroupsFitBeds", "CC_Settings_GuestGroupsFitBeds", "CC_SetNote_GuestGroupsFitBeds", "deepHooks"), HospitalityId);
            Mod(Slider("guestBedsExtra", "CC_Settings_GuestBedsExtra", "CC_SetNote_GuestBedsExtra", 0f, 4f, 1f, "0", "guestGroupsFitBeds"), HospitalityId);
            Section("CC_SetSec_Mod_RimMarket");
            Mod(Toggle("marketStallWindow", "CC_Settings_MarketStallWindow", "CC_SetNote_MarketStallWindow", "deepHooks"), RimMarketId);
            Mod(Toggle("marketStallShips", "CC_Settings_MarketStallShips", "CC_SetNote_MarketStallShips", "deepHooks"), RimMarketId);
            Mod(Toggle("marketCampersBuy", "CC_Settings_MarketCampersBuy", "CC_SetNote_MarketCampersBuy"), RimMarketId);
            // Guests shopping at the market needs both RimMarket and Hospitality.
            Mod(Toggle("marketGuestsShop", "CC_Settings_MarketGuestsShop", "CC_SetNote_MarketGuestsShop", "deepHooks"), RimMarketId, HospitalityId);
            Section("CC_SetSec_Mod_AlphaSkills");
            Mod(Toggle("drinkBeforeWork", "CC_Settings_DrinkBeforeWork", "CC_SetNote_DrinkBeforeWork", "deepHooks"), AlphaSkillsId);
            Mod(Toggle("drinkBeforeHotWork", "CC_Settings_DrinkBeforeHotWork", "CC_SetNote_DrinkBeforeHotWork", "drinkBeforeWork"), AlphaSkillsId);
            Mod(Toggle("smokeBeforeWork", "CC_Settings_SmokeBeforeWork", "CC_SetNote_SmokeBeforeWork", "deepHooks"), AlphaSkillsId);
            Section("CC_SetSec_Mod_AlphaGenes");
            Mod(Toggle("coveredLooks", "CC_Settings_CoveredLooks", "CC_SetNote_CoveredLooks", "deepHooks"), AlphaGenesId);
            Section("CC_SetSec_Mod_Magic");
            Mod(Toggle("magicGolemRepair", "CC_Settings_MagicGolemRepair", "CC_SetNote_MagicGolemRepair", "deepHooks"), MagicId);
            Mod(Toggle("magicNeedRules", "CC_Settings_MagicNeedRules", "CC_SetNote_MagicNeedRules", "deepHooks"), MagicId);
            Mod(Toggle("familiars", "CC_Settings_Familiars", "CC_SetNote_Familiars"), MagicId);
            Section("CC_SetSec_Mod_Territories");
            Mod(Toggle("territoryNewsQuiet", "CC_Settings_TerritoryNewsQuiet", "CC_SetNote_TerritoryNewsQuiet", "deepHooks"), TerritoriesId);
            Section("CC_SetSec_Mod_MoveThePatient");
            Mod(Toggle("medicalTriage", "CC_Settings_MedicalTriage", "CC_SetNote_MedicalTriage", "deepHooks"), MoveThePatientId);
            Section("CC_SetSec_Mod_PokeWorld");
            Mod(Choice("pokemonSounds", "CC_Settings_PokemonSounds", "CC_Settings_PokemonSounds_Tip", "CC_PokemonSounds_", "CC_SetNote_PokemonSounds_",
                new[] { "Creatures", "Rarer", "Own" }), PokeWorldId);
            Mod(Toggle("pokemonTypes", "CC_Settings_PokemonTypes", "CC_SetNote_PokemonTypes"), PokeWorldId);
            Mod(Toggle("pokemonTypeWarmth", "CC_Settings_PokemonTypeWarmth", "CC_SetNote_PokemonTypeWarmth", "pokemonTypes"), PokeWorldId);
            Mod(Toggle("pokemonTypeGrowth", "CC_Settings_PokemonTypeGrowth", "CC_SetNote_PokemonTypeGrowth", "pokemonTypes"), PokeWorldId);
            Mod(Toggle("pokemonTypeBattery", "CC_Settings_PokemonTypeBattery", "CC_SetNote_PokemonTypeBattery", "pokemonTypes"), PokeWorldId);
            Mod(Toggle("pokemonTypeCalm", "CC_Settings_PokemonTypeCalm", "CC_SetNote_PokemonTypeCalm", "pokemonTypes"), PokeWorldId);
            Mod(Toggle("pokemonTypeSpar", "CC_Settings_PokemonTypeSpar", "CC_SetNote_PokemonTypeSpar", "pokemonTypes"), PokeWorldId);
            Mod(Toggle("pokemonTypeDouse", "CC_Settings_PokemonTypeDouse", "CC_SetNote_PokemonTypeDouse", "pokemonTypes"), PokeWorldId);
            Section("CC_SetSec_Mod_Vte");
            Mod(Toggle("tradeVte", "CC_Settings_TradeVte", "CC_SetNote_TradeVte"), VteId);
            Mod(Toggle("tradeVteHistory", "CC_Settings_TradeVteHistory", "CC_SetNote_TradeVteHistory", "tradeVte"), VteId);
            // Any mod's faith precept against turrets (TurretFaith.cs reads the precept, not one mod): always shown.
            Section("CC_SetSec_Mod_Faiths");
            Toggle("faithTurretsHere", "CC_Settings_FaithTurretsHere", "CC_SetNote_FaithTurretsHere", "deepHooks");
        }

        /// <summary>The carrying and hauling heading: its note says so when the standalone Harvest and Haul stands in for it.</summary>
        public const string HaulingSection = "CC_SetSec_Hauling";

        // The other mods' package ids, as their integrations check them (Integrations/), for the page that shows only those loaded.
        public const string HospitalityId = "orion.hospitality", RimMarketId = "forti.rimmarket", AlphaSkillsId = "sarg.alphaskills", AlphaGenesId = "sarg.alphagenes",
            MagicId = "torann.arimworldofmagic", TerritoriesId = "jaeger972.factionterritories", MoveThePatientId = "konstantynopolitaneczka.movethepatient",
            VteId = "vanillaexpanded.vanillatradingexpanded";

        /// <summary>
        /// PokéWorld's row: its versions have had several package ids, so it is found by its Pokémon instead
        /// (SettingsAccess.ModLoaded asks PokeWorldLink.Active for this id).
        /// </summary>
        public const string PokeWorldId = "gargamiel.pokeworld";

        /// <summary>Each other mod's heading on its page, by package id: its name where a row says it isn't loaded.</summary>
        public static readonly Dictionary<string, string> ModSections = new Dictionary<string, string>
        {
            [HospitalityId] = "CC_SetSec_Mod_Hospitality",
            [RimMarketId] = "CC_SetSec_Mod_RimMarket",
            [AlphaSkillsId] = "CC_SetSec_Mod_AlphaSkills",
            [AlphaGenesId] = "CC_SetSec_Mod_AlphaGenes",
            [MagicId] = "CC_SetSec_Mod_Magic",
            [TerritoriesId] = "CC_SetSec_Mod_Territories",
            [MoveThePatientId] = "CC_SetSec_Mod_MoveThePatient",
            [VteId] = "CC_SetSec_Mod_Vte",
            [PokeWorldId] = "CC_SetSec_Mod_PokeWorld"
        };

        /// <summary>A row of the other mods' page: it shows only while all these mods are loaded.</summary>
        private static SettingEntry Mod(SettingEntry e, params string[] packageIds)
        {
            e.mods = packageIds;
            return e;
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

        /// <summary>
        /// The parts the "what's on" setups and story-rich reach: the What's on page's sections about the colony's life
        /// and its world (the council, people's lives, bonds, the story, strangers, Rimfolk's events, world events, the region,
        /// the bonds with other peoples, neighbours and letters). The tools after them (journeys, the weather, building, the
        /// planner, the automations, planning ahead, the hooks) are the player's own and no setup touches them.
        /// </summary>
        public static readonly string[] SystemSections =
        {
            "CC_Settings_Layers", "CC_SetSec_Lives", "CC_Settings_Bonds", "CC_SetSec_Story", "CC_Settings_Strangers", "CC_Settings_WorldEventsHeader",
            "CC_Settings_WorldSituationsHeader", "CC_SetSec_Region", "CC_SetSec_Peoples", "CC_SetSec_Neighbours"
        };

        /// <summary>The council and the colony's story: what "Council only" keeps on.</summary>
        public static readonly string[] CouncilSections = { "CC_Settings_Layers", "CC_SetSec_Story" };

        /// <summary>The advisor's own: on in every setup.</summary>
        public static readonly string[] AdvisorSystems = { "enableAdvisor", "nextSteps" };

        /// <summary>Every switch of a part of the colony's life, in the setups' order: the What's on page's rows under <see cref="SystemSections"/>.</summary>
        public static IEnumerable<SettingEntry> Systems => all.Where(e => e.page == SettingsPage.WhatsOn && e.kind == SettingKind.Toggle && SystemSections.Contains(e.section));

        /// <summary>A system's own switch, not one that hangs under another's (those keep their values while their parent is off).</summary>
        public static bool TopLevel(SettingEntry e) => !e.Indented;

        private static void BuildPresets()
        {
            // Each preset maps to the balance targets (DESIGN.md, "Balance targets"; Tools/Balance measures them, and
            // docs/balance-report.md has the numbers): what Rimfolk adds to a colonist's mood in a well-run, an average and
            // a neglected colony, and how often it asks a question. Since the five pages (decision 15), the three set every
            // rate on How much happens and every knob on How hard between them, and say how you're told; the region, the
            // bonds with other peoples, the heart and its good side, hurts and the gatherings a week among them.
            //
            // Light touch: Rimfolk in the background. Only upsides, gentle pressures, fewer and quieter interruptions, no
            // deadly illnesses and no fights from its events, its everyday hurts at half. Every system stays on, just slower:
            // half the travellers, raiders' camps and schemes against the colony, one gathering of its own a week, half the
            // outsiders' chatter, more time for wishes and ambitions, and no strain from diplomacy against someone's nature.
            // Its upsides (personal touches, the heart's good side) come as by default. Targets: about +4 to +7 well run, +2 to
            // +5 average, around 0 neglected; under one question every two days.
            var light = NewPreset("lightTouch", "CC_SetPreset_LightTouch");
            light.values.AddRange(new[]
            {
                // How much happens.
                V("gatheringsAWeek", "1"), V("daysBetweenWishes", "4"), V("maxOpenWishes", "3"), V("daysBetweenDilemmas", "15"), V("daysBetweenMeetings", "20"),
                V("momentFrequency", "0.75"), V("socialEventFrequency", "0.5"), V("dreamFrequency", "0.5"), V("arcFrequency", "Rare"),
                V("outsiderChatterFrequency", "0.5"), V("encounterFrequency", "0.5"), V("storytellerEventWeight", "0.5"),
                V("daysBetweenWorldEvents", "8"), V("daysBetweenWorldSituations", "75"), V("remoteTravellers", "0.5"), V("raidersCamps", "0.5"),
                V("theirSchemes", "0.5"), V("newPeoples", "0.5"), V("animalMoments", "0.5"), V("packAlphas", "0.5"), V("beastNemeses", "0.5"), V("regionLegends", "0.5"), V("greatHerds", "0.5"),
                // How hard.
                V("stakes", "Gentle"), V("wishDurationFactor", "1.25"), V("ambitionDeadlineFactor", "1.25"), V("allowQuitting", "false"),
                V("lifePressure", "Gentle"), V("frictions", "false"), V("strainSpeed", "0.75"), V("heartEpisodes", "false"), V("heartGoodSide", "true"),
                V("ailmentFrequency", "0.5"), V("seriousIllnesses", "false"), V("crowdedSickness", "false"), V("frailty", "false"), V("dirtyWounds", "false"),
                V("endOfLife", "false"), V("everydayHurts", "0.5"), V("scenarioThreat", "0.75"), V("worldEventBattles", "false"),
                // What's on: fewer questions, no strain from diplomacy against someone's nature, guests join the game's way.
                V("movingOn", "false"), V("diplomacyNature", "false"), V("guestWill", "false"),
                // How you're told.
                V("messageLevel", "LettersOnly"), V("messagesADay", "8"), V("lettersADay", "3"), V("wishLetters", "false"), V("allDreamMessages", "false")
            });

            // Story-rich: every part that tells a story switched on (every system of the colony's life and its world, the
            // region and the bonds with other peoples), and more of each: personal stories, dilemmas, events, moments,
            // dreams, personal touches, the outsiders' chatter, travellers, raiders' camps and schemes against the colony, three
            // gatherings a week, with a message for each. Wishes and meetings come as by default. Moods and pressures stay as
            // they are. Targets: the default's moods, and more questions by choice (a little over one every two days).
            var story = NewPreset("storyRich", "CC_SetPreset_StoryRich");
            foreach (var e in Systems)
                story.values.Add(V(e.id, "true"));
            story.values.AddRange(new[]
            {
                // How much happens.
                V("gatheringsAWeek", "3"), V("daysBetweenDilemmas", "5"),
                V("momentFrequency", "1.5"), V("socialEventFrequency", "1.5"), V("dreamFrequency", "1.5"), V("arcFrequency", "Frequent"),
                V("daysBetweenPersonalTouches", "4"), V("outsiderChatterFrequency", "1.5"), V("encounterFrequency", "1.5"), V("storytellerEventWeight", "1.5"),
                V("daysBetweenWorldEvents", "3"), V("daysBetweenWorldSituations", "30"), V("remoteTravellers", "1.5"), V("raidersCamps", "1.25"),
                V("theirSchemes", "1.25"), V("newPeoples", "1.25"), V("animalMoments", "1.5"), V("packAlphas", "1.25"), V("beastNemeses", "1.25"), V("regionLegends", "1.25"), V("greatHerds", "1.25"),
                // The heart's good side: brighter dreams and now and then an inspiration.
                V("heartGoodSide", "true"),
                // How you're told.
                V("speechShows", "Bubbles", "Off"), V("visualTouches", "true"), V("messageLevel", "Everything"), V("messagesADay", "16"), V("lettersADay", "6")
            });

            // Demanding: harder moods, more frictions and pressures, rarer rewards. Downsides 50% stronger and then 85% more,
            // rewards at 70%, less time to grant wishes and reach ambitions, strain building faster, and a quarter more of
            // illness, everyday hurts and the fights a scenario promises. Targets: a well-run colony about breaks even (0 to
            // +2), an average one feels it (-3 to -6), a neglected one is in real trouble (-15 to -20). (Downsides were a
            // quarter more and rewards 75%: an average colony sat near -5, a well-run one below nothing. Then 15% more and 80%
            // until phase 3 of the review, when the small letdowns' weight moved to the heart and the four softening rules
            // became one: the preset pressed a step less hard, so 85% more and 70%.)
            var hard = NewPreset("demanding", "CC_SetPreset_Demanding");
            hard.values.AddRange(new[]
            {
                V("stakes", "Demanding"), V("lifePressure", "Demanding"), V("penaltyFactor", "1.85"), V("rewardFactor", "0.7"),
                V("wishDurationFactor", "0.75"), V("ambitionDeadlineFactor", "0.75"), V("jealousy", "true"), V("wishesTakenBack", "true"), V("takenBackGrace", "Short"),
                V("allowQuitting", "true"),
                V("frictions", "true"), V("momentFrequency", "1.25"), V("risingStandards", "true"), V("goodGrowsOrdinary", "true"),
                V("quietTroubles", "true"), V("heart", "true"),
                V("heartEpisodes", "true"), V("strainSpeed", "1.25"),
                V("ailmentFrequency", "1.25"), V("seriousIllnesses", "true"), V("everydayHurts", "1.25"), V("scenarioThreat", "1.25"), V("diplomacyNature", "true")
            });

            // Default: everything the three presets above can change, back to how Rimfolk ships. Nothing else. Targets: +3
            // to +6 well run, 0 to +3 average, -4 to -10 neglected; about one question every two days or fewer.
            var normal = NewPreset("default", "CC_SetPreset_Default");
            foreach (var f in light.Fields.Concat(story.Fields).Concat(hard.Fields).Distinct())
                normal.values.Add(V(f, DefaultValue));

            // What's on (the quick setup at the top of its page): the setups the settings have always had, reaching every
            // part of the colony's life and its world, the region and the bonds with other peoples among them.
            var everything = NewPreset("everything", "CC_Preset_Everything", true);
            ScopeValues(everything, true, true);
            var core = NewPreset("councilOnly", "CC_Preset_Core", true);
            ScopeValues(core, false, true);
            var advisor = NewPreset("advisorOnly", "CC_Preset_AdvisorOnly", true);
            ScopeValues(advisor, false, false);
        }

        /// <summary>
        /// Everything on: every switch of every part on, the ones that hang under another too. Council only: the council
        /// and the colony's story on, every other part's own switch off. Advisor only: the advisor and its plan on, every
        /// other part's own switch off. A switch that hangs under another keeps its value while its parent is off, so the
        /// player's choices under it come back with it. Personal stories go to normal only if they were off; Advisor only
        /// also sets the stakes to Advisor only, the others lift the stakes out of it.
        /// </summary>
        private static void ScopeValues(SettingsPreset p, bool on, bool councilOn)
        {
            foreach (var e in Systems)
            {
                if (AdvisorSystems.Contains(e.id))
                {
                    p.values.Add(V(e.id, "true"));
                    continue;
                }
                bool keep = on || councilOn && CouncilSections.Contains(e.section);
                if (keep) p.values.Add(V(e.id, "true"));
                else if (TopLevel(e)) p.values.Add(V(e.id, "false"));
            }
            p.values.Add(on ? V("arcFrequency", "Normal", "Off") : V("arcFrequency", "Off"));
            p.values.Add(councilOn ? V("stakes", "Balanced", "AdvisorOnly") : V("stakes", "AdvisorOnly"));
        }

        private static SettingsPreset NewPreset(string id, string label, bool scope = false)
        {
            // The tips say what each covers since the five pages (decision 15): label + "_TipWhole".
            var p = new SettingsPreset { id = id, label = label, tip = label + "_TipWhole", scope = scope };
            presets.Add(p);
            return p;
        }

        private static PresetValue V(string field, string value, string onlyIfNow = null) => new PresetValue(field, value, onlyIfNow);
    }
}
