using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The words of pacts and truces (Languages/English/Keyed/Rimfolk_Pacts.xml): each by both sides' ages (the colony's
    /// word by its own age, their way by theirs: Ages.Form, the features Pact and Truce), the call by the colony's means
    /// (a fire on the hill or a horn, a rider, a radio call, comms), a truce's term and price, a pact's kind. Lines told
    /// often have numbered fellows (CC_X_2...), one picked at random.
    /// </summary>
    public static class PactWords
    {
        public static string T(string key, params NamedArgument[] args) => DipWords.T(key, args);

        public static string P(string key, params NamedArgument[] args) => DipWords.P(key, args);

        public static string People(Faction f) => DipWords.People(f);

        /// <summary>A truce's word by the colony's age (a ceasefire by comms) and their way by theirs (green branches carried between camps).</summary>
        public static AgeForm TruceForm(Map home, Faction f) => Ages.Form(AgeFeature.Truce, home ?? Homes.Seat, f);

        /// <summary>A pact's word by the colony's age (a treaty under seal) and their way by theirs (oaths sworn before witnesses).</summary>
        public static AgeForm PactForm(Map home, Faction f) => Ages.Form(AgeFeature.Pact, home ?? Homes.Seat, f);

        /// <summary>"a defence pact", "a trade pact".</summary>
        public static string Kind(PactKind k) => P("CC_Pact_Kind_" + k);

        /// <summary>
        /// A cart in words, what it is, what it is worth and what they pay: "102 plainleather a season, about 215 silver's
        /// worth; they pay 171" (one season's: without "a season"), with "(valuable)" after a valuable good (Things/Goods.cs).
        /// </summary>
        public static string CartLine(ThingDef def, int count, bool perSeason = true)
        {
            if (def == null) return "";
            string line = P(perSeason ? "CC_Pact_CartLine" : "CC_Pact_CartLineOnce", Couriers.Amount(count, null, def).Named("GOODS"),
                PactMath.CartWorth(def.BaseMarketValue, count).Named("WORTH"), PactMath.CartPay(def.BaseMarketValue, count).Named("SILVER"));
            return Goods.Valuable(def) ? line + " " + P("CC_Pact_CartValuable") : line;
        }

        /// <summary>"for a season", "for a year".</summary>
        public static string Term(TruceTerm t) => P("CC_Truce_Term_" + t);

        /// <summary>The price in words: "no price", "200 silver from the colony", "their captives returned".</summary>
        public static string Price(TrucePrice p, int silver) =>
            p == TrucePrice.OurHostage ? P("CC_Truce_Price_OurHostage", Hostages.NameOf(silver).Named("NAME")) : P("CC_Truce_Price_" + p, silver.Named("SILVER"));

        /// <summary>How the colony calls a defence pact in, by its means: a fire on the hill, a beacon for their riders, a radio call, comms.</summary>
        public static string CallByFire(Map home) => P("CC_Pact_Fire_" + Ages.Of(home ?? Homes.Seat));

        public static string CallByComms(Map home) => P("CC_Pact_Comms_" + Ages.Of(home ?? Homes.Seat));

        /// <summary>"Ada" for a person, the colony's name, a people's name.</summary>
        public static string NameOf(Ref r) => DipWords.NameOf(r);

        public static string Since(int tick) => DipWords.Since(tick);
    }
}
