using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// What the weather looks like on people and things, with the "visual and sound touches" setting, only in view:
    /// breath showing on a bitter morning (or any time in hard cold), steam off an outdoor fire's pot in the cold, steam
    /// from the sauna's door on a freezing day, and a colonist who dreads storms flinching at a lightning flash (anyone
    /// near a strike does too). Nothing is changed but what's drawn; the flinch is the game's own small jolt of a pawn.
    /// </summary>
    public static class WeatherTouches
    {
        /// <summary>Cold enough for breath to show any time, and on a morning (five to nine).</summary>
        private const float HardCold = -15f, MorningCold = -6f;

        /// <summary>Cold enough outside for a fire's pot and a sauna's door to steam.</summary>
        private const float SteamCold = 5f, SaunaCold = 0f;

        private static readonly AccessTools.FieldRef<Pawn_DrawTracker, JitterHandler> JitterOf = AccessTools.FieldRefAccess<Pawn_DrawTracker, JitterHandler>("jitterer");
        private static readonly AccessTools.FieldRef<WeatherEvent, Map> MapOf = AccessTools.FieldRefAccess<WeatherEvent, Map>("map");
        private static readonly AccessTools.FieldRef<WeatherEvent_LightningStrike, IntVec3> StrikeAt = AccessTools.FieldRefAccess<WeatherEvent_LightningStrike, IntVec3>("strikeLoc");

        private static QuirkDef stormDread;
        private static bool looked;

        // The colony's outdoor fires, found at most every 250 ticks per map.
        private static readonly Dictionary<int, (int at, List<Thing> fires)> fires = new Dictionary<int, (int, List<Thing>)>();
        private static readonly Dictionary<int, int> flinchLine = new Dictionary<int, int>();
        private static int rodLine;
        private static Game game;

        private static void Look()
        {
            if (game != Current.Game)
            {
                game = Current.Game;
                fires.Clear();
                flinchLine.Clear();
                rodLine = 0;
            }
            if (looked) return;
            looked = true;
            stormDread = DefDatabase<QuirkDef>.GetNamedSilentFail("Council_Quirk_StormDread");
        }

        /// <summary>Each touches pulse, on the map in view (VisualTouches.Pulse).</summary>
        public static void Pulse(Map map, CellRect view, int pulses)
        {
            Look();
            float outside = map.mapTemperature.OutdoorTemp;
            if (outside <= MorningCold) Breath(map, view, outside);
            if (pulses % 4 == 0 && outside <= SteamCold) FireSteam(map, view);
            if (pulses % 4 == 2 && outside <= SaunaCold) SaunaSteam(map, view);
        }

        /// <summary>A little puff at the mouth of colonists outside in the cold, now and then.</summary>
        private static void Breath(Map map, CellRect view, float outside)
        {
            int hour = GenLocalDate.HourOfDay(map);
            if (outside > HardCold && (hour < 5 || hour > 9)) return;
            foreach (var p in map.mapPawns.FreeColonistsSpawned)
            {
                if (!view.Contains(p.Position) || p.Position.Roofed(map) || !p.Awake() || !Rand.Chance(0.3f)) continue;
                var at = p.DrawPos + new Vector3(Rand.Range(-0.1f, 0.1f), 0f, 0.35f);
                var data = FleckMaker.GetDataStatic(at, map, FleckDefOf.AirPuff, Rand.Range(0.25f, 0.35f));
                data.velocityAngle = Rand.Range(-30f, 30f);
                data.velocitySpeed = Rand.Range(0.15f, 0.3f);
                map.flecks.CreateFleck(data);
            }
        }

        /// <summary>White steam rising off the colony's lit fires out in the open, in the cold.</summary>
        private static void FireSteam(Map map, CellRect view)
        {
            foreach (var fire in OutdoorFires(map))
            {
                if (fire == null || !fire.Spawned || !view.Contains(fire.Position) || !Rand.Chance(0.6f)) continue;
                var fuel = fire.TryGetComp<CompRefuelable>();
                if (fuel != null && !fuel.HasFuel) continue;
                FleckMaker.ThrowAirPuffUp(fire.DrawPos + new Vector3(Rand.Range(-0.2f, 0.2f), 0f, 0.25f), map);
            }
        }

        private static List<Thing> OutdoorFires(Map map)
        {
            int at = Find.TickManager.TicksGame / 250;
            if (fires.TryGetValue(map.uniqueID, out var kept) && kept.at == at) return kept.fires;
            var list = new List<Thing>();
            foreach (var b in map.listerBuildings.allBuildingsColonist)
                if (Sauna.IsFire(b.def) && !b.Position.Roofed(map)) list.Add(b);
            fires[map.uniqueID] = (at, list);
            return list;
        }

        /// <summary>Steam from the sauna's door on a freezing day: a wisp, and a cloud when the door stands open.</summary>
        private static void SaunaSteam(Map map, CellRect view)
        {
            foreach (var room in Sauna.Rooms(map))
            {
                if (room == null || room.Dereferenced) continue;
                foreach (var c in room.BorderCells)
                {
                    if (!c.InBounds(map) || !view.Contains(c)) continue;
                    var door = c.GetDoor(map);
                    if (door == null) continue;
                    FleckMaker.ThrowAirPuffUp(door.DrawPos + new Vector3(Rand.Range(-0.2f, 0.2f), 0f, 0.3f), map);
                    if (door.Open) FleckMaker.ThrowAirPuffUp(door.DrawPos + new Vector3(Rand.Range(-0.3f, 0.3f), 0f, 0.5f), map);
                    break;
                }
            }
        }

        /// <summary>
        /// A lightning flash on the map in view: those who dread storms flinch (and now and then mutter), and so does
        /// anyone within a few steps of a strike.
        /// </summary>
        public static void Flash(WeatherEvent e)
        {
            if (!(e is WeatherEvent_LightningFlash) || !VisualTouches.On) return;
            Look();
            var map = MapOf(e);
            if (map == null || map != Find.CurrentMap || !VisualTouches.InView(out var viewMap, out var view) || viewMap != map) return;
            IntVec3 strike = e is WeatherEvent_LightningStrike s ? StrikeAt(s) : IntVec3.Invalid;
            int now = Find.TickManager.TicksGame;
            // A strike drawn down by a lightning rod (SS Lightning Rod): someone in view is glad of it.
            if (strike.IsValid && now >= rodLine && WeatherMods.RodAt(strike, map))
            {
                var near = map.mapPawns.FreeColonistsSpawned.Where(p => view.Contains(p.Position) && p.Awake() && p.Position.InHorDistOf(strike, 25f)).RandomElementWithFallback();
                if (near != null && WeatherTalk.SayBy(near, "Council_Pool_SayLightningRod")) rodLine = now + GenDate.TicksPerHour * 6;
            }
            foreach (var p in map.mapPawns.FreeColonistsSpawned)
            {
                if (!view.Contains(p.Position) || !p.Awake()) continue;
                bool near = strike.IsValid && p.Position.InHorDistOf(strike, 8f);
                bool dreads = stormDread != null && MoreHabits.HasQuirk(p, stormDread);
                if (!near && !dreads) continue;
                var jitter = JitterOf(p.Drawer);
                jitter?.AddOffset(near ? 0.2f : 0.12f, Rand.Range(0f, 360f));
                if (!dreads || (flinchLine.TryGetValue(p.thingIDNumber, out int until) && until > now) || !Rand.Chance(0.3f)) continue;
                if (WeatherTalk.SayBy(p, "Council_Pool_SayLightningFlinch")) flinchLine[p.thingIDNumber] = now + GenDate.TicksPerHour * 3;
            }
        }
    }

    /// <summary>A lightning flash or strike is seen as it happens (Immersion/WeatherTouches.cs).</summary>
    [HarmonyPatch(typeof(WeatherEventHandler), nameof(WeatherEventHandler.AddEvent))]
    public static class Patch_LightningFlinch
    {
        public static void Postfix(WeatherEvent newEvent)
        {
            if (!RimfolkPatches.On) return;
            try
            {
                WeatherTouches.Flash(newEvent);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("lightning flinch", e);
            }
        }
    }
}
