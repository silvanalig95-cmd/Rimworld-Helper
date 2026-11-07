using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>The defs of the wild's alphas and bosses (Defs/HediffDefs/Rimfolk_Hediffs_Beasts.xml and the files beside it).</summary>
    [DefOf]
    public static class BeastsDefOf
    {
        public static HediffDef Rimfolk_PackAlpha;
        public static HediffDef Rimfolk_BeastScars;
        public static HediffDef Rimfolk_BeastOathFury;
        public static ThoughtDef Rimfolk_Alpha_Slain;
        public static ThoughtDef Rimfolk_Alpha_Tamed;
        public static ThoughtDef Rimfolk_Beast_OathKept;
        public static ThoughtDef Rimfolk_Beast_OathOthers;
        public static ThoughtDef Rimfolk_Herd_Watched;
        public static EpithetDef Rimfolk_Epithet_Packbreaker;
        public static EpithetDef Rimfolk_Epithet_GiantSlayer;
        public static EpithetDef Rimfolk_Epithet_BeastSpeaker;
        public static JobDef Rimfolk_WatchHerd;
        public static SitePartDef Rimfolk_LegendGround;

        static BeastsDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(BeastsDefOf));
        }
    }
}
