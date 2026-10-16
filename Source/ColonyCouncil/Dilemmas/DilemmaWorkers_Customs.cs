using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// What a seasonal custom needs beyond its season, on a dilemma def with <see cref="DilemmaWorker_Custom"/>: the
    /// weather outside (a swim wants a warm day, a frost soup the first cold one), water on the map, the colony's own
    /// graves, enough raw food for a feast, the land the colony stands on (a desert's rain welcome, a tundra's sun's
    /// return), rain falling, or a faith of the colony's own with one of a few memes (Ideology); and someone to ask for
    /// it whose beliefs don't rule it out.
    /// </summary>
    public class CustomConditions : DefModExtension
    {
        /// <summary>The outdoor temperature at the home map, in °C (leave out for any).</summary>
        public float minTemp = -999f;
        public float maxTemp = 999f;
        /// <summary>A river, a coast or some open water on the map.</summary>
        public bool water;
        /// <summary>At least one grave with one of the colony's own dead in it.</summary>
        public bool graves;
        /// <summary>Raw food in storage, at least this many times the dilemma's thingCount (0 = no need).</summary>
        public float foodFactor;
        /// <summary>The land, as Lands names it (desert, tundra, boreal, coast, river, lake, swamp, jungle, mountains, hills...): any one.</summary>
        public List<string> lands;
        /// <summary>Rain falling now (not snow).</summary>
        public bool raining;
        /// <summary>Ideology: the colony has a faith of its own.</summary>
        public bool faith;
        /// <summary>Ideology: the colony's faith holds one of these memes (defNames).</summary>
        public List<string> memes;
        /// <summary>Beliefs that rule it out (Wishes/Beliefs.cs): nobody whose faith despises fire asks for a bonfire.</summary>
        public List<Belief> forbiddenBy;

        [Unsaved] private ulong forbiddenMask;
        [Unsaved] private bool forbiddenMaskSet;

        /// <summary>This colonist's beliefs rule the custom out, so they won't ask for it.</summary>
        public bool AgainstBeliefsOf(Pawn p)
        {
            if (forbiddenBy.NullOrEmpty() || p == null) return false;
            if (!forbiddenMaskSet)
            {
                forbiddenMask = BeliefMath.Mask(forbiddenBy);
                forbiddenMaskSet = true;
            }
            return Beliefs.ForbidsAny(p, forbiddenMask);
        }
    }

    /// <summary>A seasonal custom: the usual season and roles, plus whatever its <see cref="CustomConditions"/> ask.</summary>
    public class DilemmaWorker_Custom : DilemmaWorker
    {
        public override bool CanHappenNow(Map map)
        {
            if (!base.CanHappenNow(map)) return false;
            var c = def.GetModExtension<CustomConditions>();
            if (c == null) return true;
            float temp = map.mapTemperature?.OutdoorTemp ?? 15f;
            if (temp < c.minTemp || temp > c.maxTemp) return false;
            if (c.water && !HasWater(map)) return false;
            if (c.graves && !HasOurGraves(map)) return false;
            if (!c.lands.NullOrEmpty() && !Lands.Fits(map, c.lands)) return false;
            if (c.raining && !Raining(map)) return false;
            if ((c.faith || !c.memes.NullOrEmpty()) && DecreeWork.ColonyFaith == null) return false;
            if (!c.memes.NullOrEmpty() && !DecreeWork.FaithHas(c.memes)) return false;
            return true;
        }

        /// <summary>Rain falling on the map now (snow doesn't count).</summary>
        public static bool Raining(Map map)
        {
            var w = map?.weatherManager?.curWeather;
            return w != null && w.rainRate > 0.1f && w.snowRate <= 0.01f;
        }

        protected override bool Extra(DilemmaContext ctx)
        {
            if (!base.Extra(ctx)) return false;
            var c = def.GetModExtension<CustomConditions>();
            if (c == null) return true;
            if (c.AgainstBeliefsOf(ctx.a)) return false;
            return c.foodFactor <= 0f || DilemmaEffect_Consume.RawFood(ctx.map) >= ctx.count * c.foodFactor;
        }

        /// <summary>A river or coast on the world tile, or open water somewhere on the map (sampled, so it stays cheap).</summary>
        public static bool HasWater(Map map)
        {
            try
            {
                if (map.TileInfo is RimWorld.Planet.SurfaceTile tile && !tile.Rivers.NullOrEmpty()) return true;
            }
            catch
            {
                // Odd tiles (space, other layers) have no rivers to ask about.
            }
            var grid = map.terrainGrid;
            int step = 7;
            for (int x = step / 2; x < map.Size.x; x += step)
                for (int z = step / 2; z < map.Size.z; z += step)
                {
                    var t = grid.TerrainAt(new IntVec3(x, 0, z));
                    if (t != null && t.IsWater) return true;
                }
            return false;
        }

        /// <summary>A grave holding one of the colony's own dead.</summary>
        public static bool HasOurGraves(Map map) =>
            map.listerBuildings.allBuildingsColonist.OfType<Building_Grave>()
                .Any(g => g.HasCorpse && g.Corpse?.InnerPawn != null && g.Corpse.InnerPawn.RaceProps.Humanlike && g.Corpse.InnerPawn.Faction == Faction.OfPlayer);
    }

    /// <summary>
    /// Ideology: certainty in the colony's faith for those of it among the effect's targets (the colony unless given),
    /// half again as much with a chaplain at home. Does nothing without Ideology or a faith of the colony's own.
    /// </summary>
    public class DilemmaEffect_Faith : DilemmaEffect
    {
        /// <summary>Certainty, as a share (0.05 = 5%).</summary>
        public float amount = 0.05f;

        public DilemmaEffect_Faith()
        {
            target = DilemmaTarget.Colony;
        }

        public override void Apply(DilemmaContext ctx)
        {
            if (DecreeWork.ColonyFaith == null || amount == 0f) return;
            float a = amount * Interplay.FaithFactor;
            foreach (var p in Targets(ctx).Distinct().ToList())
                if (DecreeWork.Believer(p)) p.ideo?.OffsetCertainty(a);
        }
    }
}
