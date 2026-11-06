using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The words of wards and hostages (Languages/English/Keyed/Rimfolk_Wards.xml): each by both sides' ages (the colony's
    /// word by its own age, their way by theirs: Ages.Form, the features Ward and Hostage), how a ward is, how the year
    /// went, the skill in a word. The Empire keeps its own: the colony's youth there is a page in a titled household. Lines
    /// told often have numbered fellows (CC_X_2...), one picked at random.
    /// </summary>
    public static class WardWords
    {
        public static string T(string key, params NamedArgument[] args) => DipWords.T(key, args);

        public static string P(string key, params NamedArgument[] args) => DipWords.P(key, args);

        public static string People(Faction f) => DipWords.People(f);

        /// <summary>A ward's word by the colony's age (an apprentice exchange) and their way by theirs (a youth in their household).</summary>
        public static AgeForm Form(Map home, Faction f) => Ages.Form(AgeFeature.Ward, home ?? Homes.Seat, f);

        /// <summary>A hostage's word by the colony's age (a guarantor) and their way by theirs (one of the clan given to another fire).</summary>
        public static AgeForm HostageForm(Map home, Faction f) => Ages.Form(AgeFeature.Hostage, home ?? Homes.Seat, f);

        /// <summary>
        /// What their hostage is among their people, for the letters and the card ("one of the traders of the Lenlia"); null
        /// for one given before the rule of 6 November 2026, or one of their people only (said as "of the Lenlia").
        /// </summary>
        public static string Standing(HostageStanding s, string people) =>
            s == HostageStanding.Trader || s == HostageStanding.Fighter ? P("CC_Hostage_Standing_" + s, people.Named("PEOPLE")) : null;

        /// <summary>How their ward is: homesick, settling in, happy.</summary>
        public static string Mood(WardMood m) => P("CC_Ward_Mood_" + m);

        /// <summary>How the year went, in a word.</summary>
        public static string Result(WardResult r) => P("CC_Ward_Result_" + r);

        /// <summary>A skill by its def name, in the game's own word for it ("crafting").</summary>
        public static string Skill(string defName)
        {
            var def = defName.NullOrEmpty() ? null : DefDatabase<SkillDef>.GetNamedSilentFail(defName);
            return def != null ? def.label : P("CC_Ward_Skill_Any");
        }

        /// <summary>"Ada" or the name the record kept.</summary>
        public static string Name(Pawn p, string kept) => p != null ? p.LabelShort : kept ?? "?";

        public static string Name(WardStay w) => Name(w?.pawn, w?.name);

        /// <summary>"their ward", "their hostage", "the colony's ward with them" and so on, for rows and cards.</summary>
        public static string What(WardStay w, Map home) =>
            w == null ? "" : w.Hostage ? HostageForm(home, w.people).word : w.page ? P("CC_Ward_Page") : Form(home, w.people).word;
    }
}
