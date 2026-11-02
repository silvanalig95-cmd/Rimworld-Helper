using System;
using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>
    /// Where an answer may not take someone, because the place would hurt them or those around them. Read from the game's
    /// own flags where it has them, and from other mods by name, quietly absent without them:
    /// <list type="bullet">
    /// <item>the sun: a gene the game marks as disliking sunlight (UV sensitivity: Biotech's, Alpha Genes' mild, vicious and
    /// lethal), or the Vanilla Expanded Framework's hediffs that burn or stage in sunlight. No open sky by day, by the
    /// game's own test for sunlight, nor where the sun will be up within two hours;</item>
    /// <item>water: Alpha Genes' thalassophobia (nothing in the game fears water). No spot within 20 cells of open water
    /// (inside a closed room, only water in that room counts: walls hide it), and no open sky in the rain;</item>
    /// <item>gas: the framework's gas-producing hediffs (tox gas, rot stink, smoke, deadlife dust breathed out every
    /// second). Nowhere under a roof, where the gas would gather round the others;</item>
    /// <item>deadlife dust besides: nowhere near a body lying in the open, which it would raise.</item>
    /// </list>
    /// The cell finders ask it of whoever they find a place for (PlaySpots.Ok), so a place is chosen to fit them; a
    /// gathering, a group or a visit leaves out those its place would hurt, and anyone at one who the place turns against
    /// (the sun coming up over a vigil, rain on a thalassophobe) goes. Their absence changes nothing else: the answer's
    /// held moods go to those who were there, as always.
    /// </summary>
    public static class PlaySafety
    {
        [Flags]
        public enum Harm
        {
            None = 0,
            Sun = 1,
            Water = 2,
            Gas = 4,
            Deadlife = 8
        }

        /// <summary>The thalassophobe's water: open water within this many cells (Alpha Genes reckons 20, in sight).</summary>
        public const float WaterCells = 20f;

        /// <summary>A body lying within this many cells of a deadlife producer may be raised.</summary>
        public const float CorpseCells = 9f;

        /// <summary>The sun counts as up if it will be within this many ticks (two hours): nobody is left out there at dawn.</summary>
        private const int SunAheadTicks = 2 * 2500;

        private const int CacheTicks = 2500;
        private static readonly Dictionary<int, (int tick, Harm harm)> cache = new Dictionary<int, (int, Harm)>();
        private static readonly Dictionary<int, (int tick, int[] dist)> water = new Dictionary<int, (int, int[])>();
        private const int WaterTicks = 60000;

        private static GeneDef thalassophobia;
        private static bool looked;

        /// <summary>While above zero, the cell finders ignore the harm (to see where a place would be without it).</summary>
        private static int ignoring;

        public static void Reset()
        {
            cache.Clear();
            water.Clear();
        }

        private static void Look()
        {
            if (looked) return;
            looked = true;
            thalassophobia = DefDatabase<GeneDef>.GetNamedSilentFail("AG_Thalassophobia");
        }

        /// <summary>What places would hurt this person (or those near them), read at most once an hour.</summary>
        public static Harm Of(Pawn p)
        {
            if (p == null || p.Dead) return Harm.None;
            int now = Find.TickManager?.TicksGame ?? 0;
            if (cache.TryGetValue(p.thingIDNumber, out var c) && now >= c.tick && now - c.tick < CacheTicks) return c.harm;
            Harm h;
            try
            {
                h = Read(p);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("play safety", e);
                h = Harm.None;
            }
            if (cache.Count > 600) cache.Clear();
            cache[p.thingIDNumber] = (now, h);
            return h;
        }

        private static Harm Read(Pawn p)
        {
            Look();
            var h = Harm.None;
            if (ModsConfig.BiotechActive && p.genes != null)
                foreach (var g in p.genes.GenesListForReading)
                {
                    if (g?.def == null || !g.Active) continue;
                    if (g.def.dislikesSunlight) h |= Harm.Sun;
                    if (thalassophobia != null && g.def == thalassophobia) h |= Harm.Water;
                }
            var hediffs = p.health?.hediffSet?.hediffs;
            if (hediffs == null) return h;
            foreach (var hd in hediffs)
            {
                if (!(hd is HediffWithComps hc) || hc.comps == null) continue;
                foreach (var comp in hc.comps)
                {
                    var props = comp?.props;
                    if (props == null) continue;
                    switch (props.GetType().Name)
                    {
                        // Burns in sunlight (lethal UV sensitivity), rather than when already on fire.
                        case "HediffCompProperties_HighlyFlammable":
                            if (Field(props, "sunlightBurns") is bool burns && burns) h |= Harm.Sun;
                            break;
                        // A stage for sunlight that takes something away (vicious UV sensitivity: knocked out).
                        case "HediffCompProperties_StageBySunlight":
                            if (Field(props, "sunlightStageIndex") is float sev && Hurts(hd.def, sev)) h |= Harm.Sun;
                            break;
                        case "HediffCompProperties_GasProducer":
                            h |= Harm.Gas;
                            if (Field(props, "gasType") is GasType gas && gas == GasType.DeadlifeDust) h |= Harm.Deadlife;
                            break;
                    }
                }
            }
            return h;
        }

        private static readonly Dictionary<(Type, string), FieldInfo> fields = new Dictionary<(Type, string), FieldInfo>();

        private static object Field(object o, string name)
        {
            var t = o.GetType();
            if (!fields.TryGetValue((t, name), out var f))
                fields[(t, name)] = f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return f?.GetValue(o);
        }

        /// <summary>The hediff's stage at this severity lowers a capacity (an offset below zero, a cap under full).</summary>
        private static bool Hurts(HediffDef def, float severity)
        {
            if (def?.stages == null || def.stages.Count == 0) return false;
            int i = def.StageAtSeverity(severity);
            if (i < 0 || i >= def.stages.Count || def.stages[i]?.capMods == null) return false;
            foreach (var m in def.stages[i].capMods)
                if (m != null && (m.offset < 0f || (m.SetMaxDefined && m.setMax < 1f) || (m.postFactor > 0f && m.postFactor < 1f))) return true;
            return false;
        }

        // ---------------- Places ----------------

        /// <summary>A place they may be taken to: nothing about it would hurt them (or, for their gas, those with them).</summary>
        public static bool Safe(Pawn p, IntVec3 c, Map map) => SafeFrom(p, c, map, true);

        /// <summary>
        /// The place they're on their way to for an answer, or at: a gathering's spot in full; the far end of an errand or a
        /// walk without the gas, which only matters where people gather round them. False when it would hurt them now.
        /// </summary>
        public static bool SafeWhereHeaded(Pawn p)
        {
            if (p?.Map == null || !p.Spawned || Of(p) == Harm.None) return true;
            var map = p.Map;
            if (p.GetLord()?.LordJob is LordJob_PlayOut gathering) return Safe(p, gathering.Spot, map);
            // On one of Rimfolk's own jobs (a walk, a vigil, an errand): where it takes them.
            var job = p.CurJob;
            if (job?.def?.driverClass == null || job.def.driverClass.Namespace != typeof(PlaySafety).Namespace) return true;
            var at = p.pather != null && p.pather.Moving ? p.pather.Destination.Cell : p.Position;
            return SafeFrom(p, at, map, false);
        }

        private static bool SafeFrom(Pawn p, IntVec3 c, Map map, bool gas) => HarmAt(p, c, map, gas) == Harm.None;

        /// <summary>What about this place would hurt them (or, with <paramref name="gas"/>, those with them): the first that does, or none.</summary>
        public static Harm HarmAt(Pawn p, IntVec3 c, Map map, bool gas = true)
        {
            if (p == null || map == null || ignoring > 0 || !c.IsValid || !c.InBounds(map)) return Harm.None;
            var h = Of(p);
            if (h == Harm.None) return Harm.None;
            bool roofed = c.Roofed(map);
            if ((h & Harm.Sun) != 0 && !roofed && SunUp(c, map)) return Harm.Sun;
            if ((h & Harm.Water) != 0 && ((!roofed && Raining(map)) || NearWater(c, map))) return Harm.Water;
            if (gas && (h & Harm.Gas) != 0 && roofed) return Harm.Gas;
            if ((h & Harm.Deadlife) != 0 && CorpseNear(c, map)) return Harm.Deadlife;
            return Harm.None;
        }

        /// <summary>Why they stayed away, for a meeting's letter ("kept out of the sun").</summary>
        public static string Why(Harm h)
        {
            switch (h)
            {
                case Harm.Sun: return "CC_MeetWhy_Sun".Translate();
                case Harm.Water: return "CC_MeetWhy_Water".Translate();
                case Harm.Gas: return "CC_MeetWhy_Gas".Translate();
                case Harm.Deadlife: return "CC_MeetWhy_Dead".Translate();
                default: return null;
            }
        }

        /// <summary>The sun on this open cell now, by the game's own test, or up within two hours.</summary>
        private static bool SunUp(IntVec3 c, Map map)
        {
            try
            {
                if (SanguophageUtility.InSunlight(c, map)) return true;
                return GenCelestial.IsDaytime(GenCelestial.CelestialSunGlow(map, Find.TickManager.TicksAbs + SunAheadTicks));
            }
            catch
            {
                return false;
            }
        }

        /// <summary>The sun is up over the map, or will be within two hours.</summary>
        public static bool DayOut(Map map)
        {
            if (map == null) return false;
            try
            {
                return GenCelestial.IsDaytime(GenCelestial.CurCelestialSunGlow(map))
                       || GenCelestial.IsDaytime(GenCelestial.CelestialSunGlow(map, Find.TickManager.TicksAbs + SunAheadTicks));
            }
            catch
            {
                return false;
            }
        }

        public static bool Raining(Map map) => (map?.weatherManager?.curWeather?.rainRate ?? 0f) > 0f;

        /// <summary>
        /// Open water in sight within 20 cells: in a closed room, water in the room itself (its walls hide the rest); outside,
        /// any water that near (from a grid worked out once a day for the map).
        /// </summary>
        public static bool NearWater(IntVec3 c, Map map)
        {
            var room = c.GetRoom(map);
            if (room != null && !room.PsychologicallyOutdoors && room.ProperRoom && room.CellCount <= 1200)
            {
                foreach (var x in room.Cells)
                    if (x.GetTerrain(map)?.IsWater ?? false) return true;
                return false;
            }
            var dist = WaterGrid(map);
            if (dist == null) return false;
            int i = map.cellIndices.CellToIndex(c);
            return i >= 0 && i < dist.Length && BodyMath.WithinCells(dist[i], WaterCells);
        }

        private static int[] WaterGrid(Map map)
        {
            int now = Find.TickManager?.TicksGame ?? 0;
            if (water.TryGetValue(map.uniqueID, out var w) && now >= w.tick && now - w.tick < WaterTicks) return w.dist;
            int sx = map.Size.x, sz = map.Size.z;
            var wet = new bool[sx * sz];
            var grid = map.terrainGrid;
            for (int i = 0; i < wet.Length; i++) wet[i] = grid.TerrainAt(i)?.IsWater ?? false;
            var dist = BodyMath.WaterDistance(wet, sx, sz, (int)(WaterCells * BodyMath.Straight) + BodyMath.Diagonal);
            if (water.Count > 8) water.Clear();
            water[map.uniqueID] = (now, dist);
            return dist;
        }

        /// <summary>A body lying in the open near the cell (one in a grave or a sarcophagus isn't on the map).</summary>
        private static bool CorpseNear(IntVec3 c, Map map)
        {
            var corpses = map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse);
            for (int i = 0; i < corpses.Count; i++)
            {
                var t = corpses[i];
                if (t != null && t.Spawned && t.Position.InHorDistOf(c, CorpseCells)) return true;
            }
            return false;
        }

        // ---------------- Plans ----------------

        /// <summary>
        /// Whether a group may take them at all, before its place is known: no plan out under the open sky by day for one
        /// the sun burns; none to the water, or out in the rain, for a thalassophobe; no vigil at someone's bedside for one
        /// who breathes out gas; no carrying of the dead for one whose dust would raise them. Where it goes is asked of
        /// each of them again on the way (PlayOuts.CanJoin).
        /// </summary>
        public static bool CanGo(PlayOut p, Pawn x, bool outside)
        {
            if (p == null || x == null) return true;
            var h = Of(x);
            if (h == Harm.None) return true;
            var map = p.map;
            bool fields = p.kind == PlayKind.WorkParty && p.tasks != null && p.tasks.Exists(OutdoorWork);
            if ((h & Harm.Sun) != 0 && (outside || fields) && DayOut(map)) return false;
            bool wet = p.to == PlaySpot.Water || (p.kind == PlayKind.WorkParty && p.tasks != null && p.tasks.Contains(WorkTask.Fish));
            if ((h & Harm.Water) != 0 && (wet || ((outside || fields) && Raining(map)))) return false;
            if ((h & Harm.Gas) != 0 && p.kind == PlayKind.Vigil && p.to == PlaySpot.Person) return false;
            if ((h & Harm.Deadlife) != 0 && (p.kind == PlayKind.Haul || (p.kind == PlayKind.WorkParty && p.tasks != null && p.tasks.Contains(WorkTask.Corpses)))) return false;
            // Something carried, handed over or watched over at someone's side goes to where they are now.
            var o = p.other;
            bool toThem = p.kind == PlayKind.Carry || p.kind == PlayKind.Give || (p.kind == PlayKind.Vigil && p.to == PlaySpot.Person);
            if (toThem && o != null && o.Spawned && o.Map == map && HarmAt(x, o.Position, map, false) != Harm.None) return false;
            return true;
        }

        /// <summary>A work party's work out under the sky: the fields, the trees, the snow, the herd, the fishing.</summary>
        private static bool OutdoorWork(WorkTask t) =>
            t == WorkTask.Sow || t == WorkTask.Harvest || t == WorkTask.Cut || t == WorkTask.Wood || t == WorkTask.Snow || t == WorkTask.Herd || t == WorkTask.Fish;

        /// <summary>While it lasts, the cell finders find places as if nobody were hurt by any (to see where a plan would be held).</summary>
        public static Ignoring Ignore() => new Ignoring(true);

        public readonly struct Ignoring : IDisposable
        {
            private readonly bool on;

            public Ignoring(bool on)
            {
                this.on = on;
                if (on) ignoring++;
            }

            public void Dispose()
            {
                if (on && ignoring > 0) ignoring--;
            }
        }
    }
}
