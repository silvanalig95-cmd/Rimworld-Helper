using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The words of fealty and vassals (Languages/English/Keyed/Rimfolk_Fealty.xml): the oath's word by the colony's age
    /// and their way by theirs (Ages.Form, the feature Fealty: tribute to a stronger clan and protection for tribute, a
    /// protectorate and a company town, a charter and a corporate accord), the levies' (the feature Levy), the hamlet's
    /// protection (the feature Hamlet), a vassal head's title, the tribute's levels, and the terms told by their ways with
    /// the bad sides named. Lines told often have numbered fellows (CC_X_2...), one picked at random.
    /// </summary>
    public static class FealtyWords
    {
        public static string T(string key, params NamedArgument[] args) => DipWords.T(key, args);

        public static string P(string key, params NamedArgument[] args) => DipWords.P(key, args);

        public static string People(Faction f) => DipWords.People(f);

        /// <summary>Fealty's word by the colony's age (a protectorate) and their way by theirs (an oath of fealty and levies).</summary>
        public static AgeForm Form(Map home, Faction f) => Ages.Form(AgeFeature.Fealty, home ?? Homes.Seat, f);

        /// <summary>The levies' word by the colony's age (the clan's spears, a militia) and their way by theirs (men at arms raised).</summary>
        public static AgeForm LevyForm(Map home, Faction f) => Ages.Form(AgeFeature.Levy, home ?? Homes.Seat, f);

        /// <summary>The colony's protection of its vassals by its own age (the clan's spear, a lord's protection, a protectorate's guarantee, a charter of protection).</summary>
        public static AgeForm ProtectionForm(Map home, Faction f) => Ages.Form(AgeFeature.Hamlet, home ?? Homes.Seat, f);

        /// <summary>"light", "usual", "heavy".</summary>
        public static string Level(TributeLevel l) => P("CC_Vassal_Level_" + l);

        /// <summary>
        /// The liege's terms in their own ways, for the letter and the dialog: tribute each season (about so much silver's
        /// worth now), levies when called (two fighters for up to five days), and their protection when a raid comes.
        /// </summary>
        public static string Terms(Faction f, Map home)
        {
            var form = Form(home, f);
            var levy = LevyForm(home, f);
            return P("CC_Fealty_Terms", People(f).Named("PEOPLE"), form.rite.Named("RITE"), Fealty.TributeValue(home).Named("TRIBUTE"), levy.rite.Named("LEVY"),
                FealtyMath.LevyFighters.Named("COUNT"), ((int)FealtyMath.LevyDays).Named("DAYS"));
        }

        /// <summary>The bad sides, named before the choice: tribute in a lean season, levies hurt or killed in their war, renouncing's cost.</summary>
        public static string Risks(Faction f, Map home) =>
            P("CC_Fealty_Risks", People(f).Named("PEOPLE"), FealtyMath.BrokenGoodwill.Named("BROKEN"), FealtyMath.LeviesRefusedGoodwill.Named("REFUSED"),
                FealtyMath.RenounceGoodwill.Named("RENOUNCE"));

        public static string Since(int tick) => DipWords.Since(tick);
    }
}
