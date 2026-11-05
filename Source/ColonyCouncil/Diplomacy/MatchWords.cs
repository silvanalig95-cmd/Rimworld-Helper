using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The words of marriage alliances (Languages/English/Keyed/Rimfolk_Matches.xml): a match by both sides' ages (the
    /// colony's word by its own age: a match between clans, houses, families or firms, a partnership contract between lines;
    /// their way by theirs: bride gifts and a shared fire, vows in the hall and a dowry, a wedding and papers signed, a
    /// contract witnessed and filed: Ages.Form, the feature Marriage), their faith's own wedding where Ideology gives them one
    /// (InviteForms.Rite), where the couple lives, the colonist's consent, how the colony wronged a spouse. Lines told often
    /// have numbered fellows (CC_X_2...), one picked at random.
    /// </summary>
    public static class MatchWords
    {
        public static string T(string key, params NamedArgument[] args) => DipWords.T(key, args);

        public static string P(string key, params NamedArgument[] args) => DipWords.P(key, args);

        public static string People(Faction f) => DipWords.People(f);

        /// <summary>The match's word by the colony's age and their way by theirs.</summary>
        public static AgeForm Form(Map home, Faction f) => Ages.Form(AgeFeature.Marriage, home ?? Homes.Seat, f);

        /// <summary>How their people hold a wedding: their faith's own (Ideology), else their age's way.</summary>
        public static string Rite(Faction f, Map home)
        {
            string faith = InviteForms.Rite(f, home ?? Homes.Seat, "wedding", out bool isFaith);
            return isFaith && !faith.NullOrEmpty() ? faith : Form(home, f).rite;
        }

        /// <summary>A pawn's short name, or the name kept for one gone.</summary>
        public static string Name(Pawn p, string kept) => p != null && !p.Discarded ? p.LabelShort : kept ?? "?";

        /// <summary>Where the couple will live, in a phrase: "Bram comes to live at the colony", "Ada goes to live at Tallbrook".</summary>
        public static string Lives(MatchHome h, string ours, string theirs, string town) =>
            P(h == MatchHome.Colony ? "CC_Match_Lives_Colony" : "CC_Match_Lives_Theirs", ours.Named("OURS"), theirs.Named("THEIRS"), (town ?? "?").Named("TOWN"));

        /// <summary>A colonist's feelings about it, in a sentence: glad, willing, or against it and why.</summary>
        public static string ConsentLine(Pawn ours, Consent c, ConsentWhy why, Pawn other)
        {
            if (ours == null) return "";
            if (c == Consent.Glad) return P("CC_Match_Consent_Glad", ours.Named("PAWN"));
            if (c == Consent.Willing) return P("CC_Match_Consent_Willing", ours.Named("PAWN"));
            return P("CC_Match_Consent_Against_" + why, ours.Named("PAWN"), (other?.LabelShort ?? "?").Named("OTHER"));
        }

        /// <summary>How the colony wronged a spouse, in a phrase ("killed by a colonist's hand").</summary>
        public static string Wrong(MatchWrong w) => P("CC_Match_Wrong_" + w);

        /// <summary>One of theirs for a letter's topic: "Bram, heir of Tallbrook", or "a daughter of Reeve Hallam's house".</summary>
        public static string Candidate(Pawn theirs, Notable n, Notable house, Gender g)
        {
            if (theirs != null)
            {
                string title = n != null ? NotableBook.TitleOf(n) : null;
                return title.NullOrEmpty() ? theirs.LabelShort : P("CC_Match_Candidate_Notable", theirs.Named("THEIRS"), title.Named("TITLE"), (n.townName ?? "?").Named("TOWN"));
            }
            return P(g == Gender.Female ? "CC_Match_Candidate_Daughter" : "CC_Match_Candidate_Son", NotableBook.Called(house).Named("HEAD"));
        }
    }
}
