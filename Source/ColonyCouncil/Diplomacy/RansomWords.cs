using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The words of ransom and the envoy (Languages/English/Keyed/Rimfolk_Ransom.xml): each by both sides' ages (the
    /// colony's word by its own age, their way by theirs: Ages.Form, the features Ransom and Envoy), a price in silver or
    /// a tribe's goods, the trap's odds in words. Lines told often have numbered fellows (CC_X_2...), one picked at random.
    /// </summary>
    public static class RansomWords
    {
        public static string T(string key, params NamedArgument[] args) => DipWords.T(key, args);

        public static string P(string key, params NamedArgument[] args) => DipWords.P(key, args);

        public static string People(Faction f) => DipWords.People(f);

        /// <summary>Ransom's word by the colony's age (ransom by messenger) and their way by theirs (captives for goods).</summary>
        public static AgeForm Form(Map home, Faction f) => Ages.Form(AgeFeature.Ransom, home ?? Homes.Seat, f);

        /// <summary>The envoy's word by the colony's age (a consul) and their way by theirs (letters of credence).</summary>
        public static AgeForm EnvoyForm(Map home, Faction f) => Ages.Form(AgeFeature.Envoy, home ?? Homes.Seat, f);

        /// <summary>"300 silver", "20 pemmican".</summary>
        public static string Price(int silver, string goods)
        {
            var def = goods.NullOrEmpty() ? null : DefDatabase<ThingDef>.GetNamedSilentFail(goods);
            if (def == null) return P("CC_Ransom_Silver", silver.Named("SILVER"));
            return Couriers.Amount(RansomMath.GoodsCount(silver, def.BaseMarketValue), null, def);
        }

        /// <summary>The trap's chance in words, with the one who speaks for the colony.</summary>
        public static string Odds(float chance) => P("CC_Ransom_Odds_" + RansomMath.Odds(chance));

        /// <summary>How their envoy's stay goes, in a word.</summary>
        public static string Mood(EnvoyMood m) => P("CC_Envoy_Mood_" + m);

        /// <summary>"Ada" or the name the record kept.</summary>
        public static string Name(Pawn p, string kept) => p != null ? p.LabelShort : kept ?? "?";
    }
}
