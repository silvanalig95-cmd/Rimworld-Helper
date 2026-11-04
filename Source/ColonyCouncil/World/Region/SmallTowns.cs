using System;
using HarmonyLib;
using RimWorld;
using RimWorld.BaseGen;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Small towns look small (docs/design/stage1-region.md, decision 4): the map of a hamlet, a trading post or a town in
    /// its first year, made when it is attacked or entered, has a smaller base with fewer defenders and less in its stores
    /// (<see cref="SmallTownsMath"/>). It changes only what the base's builder is handed: the ground it builds on, shrunk
    /// around its middle, the defenders' points and the stockpile's worth. So the game builds a smaller base of its own,
    /// and Vanilla Base Generation Expanded (whose settlements come from each people's layouts, built by the Vanilla
    /// Expanded Framework's "kcsg_settlement" inside the ground it is handed, defenders at that people's own multiplier)
    /// builds its own layouts on the smaller ground. Only the game's own settlements of other peoples: sites, ruins, Real
    /// Ruins' places, Faction Territories' and Vanilla Outposts Expanded's world objects and Factional War's battles are
    /// never touched; an Empire town or one that needs a gravcore room keeps its ground, and only its defenders are fewer.
    /// </summary>
    public static class SmallTowns
    {
        /// <summary>The setting "Small towns' maps" (with the deeper game hooks).</summary>
        public static bool On => (ColonyCouncilMod.Settings?.smallTownMaps ?? true) && RimfolkPatches.On;

        /// <summary>The game's own base, and the Vanilla Expanded Framework's (Vanilla Base Generation Expanded's layouts).</summary>
        public const string Vanilla = "settlement", Layouts = "kcsg_settlement";

        /// <summary>What kind of small town a settlement is, by the ledger's record: a hamlet, a post, a town in its first year, or none.</summary>
        public static SmallTown KindOf(Settlement s)
        {
            if (s == null || s.Destroyed || s.Faction == null || s.Faction.IsPlayer || s.def != WorldObjectDefOf.Settlement) return SmallTown.None;
            var rec = WorldLedger.TownRecord(s.ID);
            if (rec == null || rec.gone >= 0) return SmallTown.None;
            return SmallTownsMath.Of(rec.kind, RegionMath.IsNew(rec.founded, Find.TickManager?.TicksGame ?? 0));
        }

        /// <summary>The base being built now is a small town's: shrink what its builder is handed.</summary>
        internal static void Shrink(string symbol, ref ResolveParams rp)
        {
            var map = BaseGen.globalSettings.map;
            if (!(map?.Parent is Settlement s)) return;
            var kind = KindOf(s);
            if (kind == SmallTown.None) return;
            bool layouts = symbol == Layouts;
            // Its defenders: a share of a town's, through the builder's own multiplier after this.
            rp.settlementPawnGroupPoints = SmallTownsMath.Points(rp.settlementPawnGroupPoints ?? SymbolResolver_Settlement.DefaultPawnsPoints.RandomInRange, kind);
            if (!layouts) rp.lootMarketValue = (rp.lootMarketValue ?? SymbolResolver_Settlement.DefaultLootMarketValue) * SmallTownsMath.LootFactor(kind);
            // An Empire town (its throne room and landing pad) or one that needs a gravcore room keeps the ground it has.
            if (Ages.IsEmpire(s.Faction) || BaseGen.globalSettings.requiredGravcoreRooms > 0) return;
            var r = rp.rect;
            float f = SmallTownsMath.SizeFactor(kind);
            int w = SmallTownsMath.Side(r.Width, f), h = SmallTownsMath.Side(r.Height, f);
            if (w == r.Width && h == r.Height) return;
            rp.rect = new CellRect(SmallTownsMath.Start(r.minX, r.Width, w), SmallTownsMath.Start(r.minZ, r.Height, h), w, h);
            if (!layouts && SmallTownsMath.NoEdgeDefense(kind)) rp.edgeDefenseWidth = 0;
        }
    }

    /// <summary>
    /// The builder of a settlement's base handed its ground (the game's "settlement", the Vanilla Expanded Framework's
    /// "kcsg_settlement"): a small town's ground, defenders and stores made smaller (<see cref="SmallTowns"/>). Every other
    /// symbol, and every other map, passes untouched; one comparison of a name for them.
    /// </summary>
    [HarmonyPatch(typeof(SymbolStack), nameof(SymbolStack.Push), new[] { typeof(string), typeof(ResolveParams), typeof(string) })]
    public static class Patch_SmallTownMap
    {
        public static void Prefix(string symbol, ref ResolveParams resolveParams)
        {
            if (symbol != SmallTowns.Vanilla && symbol != SmallTowns.Layouts || !SmallTowns.On) return;
            try
            {
                SmallTowns.Shrink(symbol, ref resolveParams);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("small towns: a smaller base", e);
            }
        }
    }
}
