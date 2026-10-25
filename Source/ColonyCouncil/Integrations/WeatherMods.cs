using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Other weather mods, met where Rimfolk already meets the weather (each only with its mod, and safe to add to a
    /// running save): Stormproof's heat dome and polar front are talked about like a heat wave and a cold snap, and its
    /// five storms send a camp on its way and are in the advisor's "what's happening now"; one who loves the rain
    /// (Weather is kinda cozy's Pluviophile) never comes to dread storms; a strike drawn to a lightning rod (SS Lightning
    /// Rod) gets a word from someone in view; a tornado crossing the land (Disasters: Tornado, or the game's own) is
    /// written into the chronicle and a journal; and Rimfolk's fireflies stand aside for Quiet Wonders' own.
    /// </summary>
    public static class WeatherMods
    {
        public const string QuietWonders = "choco.quietwonders";

        private static TraitDef pluviophile;
        private static ThingDef rod, rodBasic, tornado;
        private static bool looked;

        private static void Look()
        {
            if (looked) return;
            looked = true;
            pluviophile = DefDatabase<TraitDef>.GetNamedSilentFail("Pluviophile");
            rod = DefDatabase<ThingDef>.GetNamedSilentFail("LightningRod");
            rodBasic = DefDatabase<ThingDef>.GetNamedSilentFail("LightningRodBasic");
            tornado = DefDatabase<ThingDef>.GetNamedSilentFail("Tornado");
        }

        /// <summary>Stormproof's heat dome: like a heat wave, only worse.</summary>
        public static bool HeatLike(GameConditionDef d) => d != null && d.defName == "Stormproof_HeatDome";

        /// <summary>Stormproof's polar front: like a cold snap, only worse.</summary>
        public static bool ColdLike(GameConditionDef d) => d != null && d.defName == "Stormproof_PolarFront";

        /// <summary>One of Stormproof's storms, which no camp would sit out on open ground.</summary>
        public static bool StormproofStorm(GameConditionDef d) =>
            d != null && (d.defName == "Stormproof_HeatDome" || d.defName == "Stormproof_PolarFront" || d.defName == "Stormproof_ToxicSurge"
                          || d.defName == "Stormproof_DryLightning" || d.defName == "Stormproof_IonStorm");

        /// <summary>A quirk this colonist won't pick up: one who loves the rain doesn't come to dread storms.</summary>
        public static bool Unfit(QuirkDef q, Pawn p)
        {
            if (q == null || q.defName != "Council_Quirk_StormDread") return false;
            Look();
            return pluviophile != null && (p?.story?.traits?.HasTrait(pluviophile) ?? false);
        }

        /// <summary>A lightning rod stands where the strike came down.</summary>
        public static bool RodAt(IntVec3 c, Map map)
        {
            Look();
            if ((rod == null && rodBasic == null) || !c.InBounds(map)) return false;
            foreach (var t in c.GetThingList(map))
                if (t.def == rod || t.def == rodBasic) return true;
            return false;
        }

        /// <summary>A tornado is on this map now.</summary>
        public static bool TornadoOn(Map map)
        {
            Look();
            return tornado != null && map.listerThings.ThingsOfDef(tornado).Count > 0;
        }

        /// <summary>Quiet Wonders draws fireflies of its own: Rimfolk's stand aside.</summary>
        public static bool OwnFireflies => ModsConfig.IsActive(QuietWonders);
    }
}
