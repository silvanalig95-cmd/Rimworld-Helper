using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// How near each god is to the colony at a home, read off the game (GodNearMath has the rules): the situation on the
    /// land (its shape's stirs, GodShapeDef.nearWhen), its relations (a quarrel it is in, the gods tied to it stirred) and
    /// what the colony does (offerings, harm, its book, a strong standing). Hidden state: never shown or told; the player
    /// sees only what a near god does. Worked out at most once an hour for each god at each home.
    /// </summary>
    public static class GodNear
    {
        private static readonly Dictionary<(int god, int home), (int at, float near)> cache = new Dictionary<(int, int), (int, float)>();
        private static readonly Dictionary<(int map, GodStir stir), (int at, float v)> stirs = new Dictionary<(int, GodStir), (int, float)>();

        /// <summary>The dev tools' nearness for a god (until the game is left), or none.</summary>
        private static readonly Dictionary<int, float> forced = new Dictionary<int, float>();

        private static Game game;

        private static int Now => Find.TickManager?.TicksGame ?? 0;

        private static void Fresh()
        {
            if (Current.Game == game) return;
            game = Current.Game;
            cache.Clear();
            stirs.Clear();
            forced.Clear();
        }

        /// <summary>How near a god is at a home now (0 felt, 1 as near as a god comes).</summary>
        public static float Of(God g, Map home)
        {
            if (g == null || !Gods.On) return 0f;
            Fresh();
            if (forced.TryGetValue(g.id, out float f)) return f;
            var map = home != null && Homes.IsHome(home) ? home : Homes.Seat;
            int key = Gods.HomeKey(map);
            int now = Now;
            if (cache.TryGetValue((g.id, key), out var c) && now - c.at >= 0 && now - c.at < GenDate.TicksPerHour) return c.near;
            float near = 0f;
            try
            {
                near = GodNearMath.Nearness(Situation(g, map), Relations(g, map), Colony(g, map, key, now));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: how near a god is", e);
            }
            cache[(g.id, key)] = (now, near);
            return near;
        }

        /// <summary>The god shows plainly this time (a roll on its nearness: GodNearMath.PlainChance).</summary>
        public static bool Plain(God g, Map home) => g != null && GodNearMath.Plain(Of(g, home), Rand.Value);

        /// <summary>The god is drawn near enough to be spoken of as near (lines tagged near).</summary>
        public static bool Near(God g, Map home) => g != null && GodNearMath.Near(Of(g, home));

        /// <summary>How far from felt toward walking its acts are now (GodNearMath.Strength).</summary>
        public static float Strength(God g, Map home) => GodNearMath.Strength(Of(g, home));

        // ================= The situation =================

        /// <summary>How strongly the situation that draws this god holds at the home: the most of its shape's stirs.</summary>
        public static float Situation(God g, Map map)
        {
            if (g == null || map == null) return 0f;
            var list = GodShapes.Of(g).nearWhen;
            if (list.NullOrEmpty()) return 0f;
            float most = 0f;
            foreach (var s in list) most = Math.Max(most, Stir(map, s));
            return most;
        }

        /// <summary>How strongly a stir holds on a map now (0 to 1), worked out at most once an hour.</summary>
        public static float Stir(Map map, GodStir s)
        {
            if (map == null) return 0f;
            Fresh();
            int now = Now;
            if (stirs.TryGetValue((map.uniqueID, s), out var c) && now - c.at >= 0 && now - c.at < GenDate.TicksPerHour) return c.v;
            float v = 0f;
            try
            {
                v = Read(map, s);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: a god's stir " + s, e);
            }
            stirs[(map.uniqueID, s)] = (now, v);
            return v;
        }

        private static bool Active(Map map, GameConditionDef def) => def != null && map.gameConditionManager != null && map.gameConditionManager.ConditionIsActive(def);

        private static float Read(Map map, GodStir s)
        {
            float temp = map.mapTemperature?.OutdoorTemp ?? 15f;
            switch (s)
            {
                case GodStir.Drought:
                {
                    var book = WeatherBook.Of(map);
                    int dry = 0;
                    if (book?.DaysBefore != null)
                        for (int i = book.DaysBefore.Count - 1; i >= 0 && book.DaysBefore[i].Dry; i--)
                            dry++;
                    return Math.Max(Active(map, GameConditionDefOf.Drought) ? 1f : 0f, GodNearMath.DroughtStir(dry));
                }
                case GodStir.DeepMine:
                {
                    var b = GodsOnMapBook.Instance;
                    return b == null ? 0f : GodNearMath.DeepStir(b.deepWarnings, GodsOnMapMath.WakesAt, b.woken != null && b.woken.Spawned && b.woken.Map == map);
                }
                case GodStir.HardWinter:
                    return GodNearMath.WinterStir(temp, Active(map, GameConditionDefOf.ColdSnap));
                case GodStir.Storm:
                {
                    if (Active(map, GameConditionDefOf.Flashstorm)) return 1f;
                    var k = WeatherDynamics.KindOf(map.weatherManager?.curWeather);
                    return k == WeatherKind.Storm || k == WeatherKind.DryStorm ? 0.8f : 0f;
                }
                case GodStir.FullMoon:
                    return GodOmens.MoonTonight == GodsOnMapMath.MoonFace.Full ? 0.8f : GodOmens.MoonTonight == GodsOnMapMath.MoonFace.Half ? 0.3f : 0f;
                case GodStir.DarkMoon:
                    return GodOmens.MoonTonight == GodsOnMapMath.MoonFace.Gone ? 0.7f : GodOmens.MoonTonight == GodsOnMapMath.MoonFace.Thin ? 0.25f : 0f;
                case GodStir.Heat:
                    return GodNearMath.HeatStir(temp, Active(map, GameConditionDefOf.HeatWave) || Active(map, DefDatabase<GameConditionDef>.GetNamedSilentFail("SolarFlare")));
                case GodStir.Growth:
                {
                    var anima = DefDatabase<ThingDef>.GetNamedSilentFail("Plant_TreeAnima");
                    float a = anima != null && map.listerThings.ThingsOfDef(anima).Count > 0 ? 0.6f : 0f;
                    var season = GenLocalDate.Season(map);
                    float g = (season == Season.Summer || season == Season.Spring || season == Season.PermanentSummer) && temp >= 10f && temp <= 35f ? 0.3f : 0f;
                    return Math.Max(a, g);
                }
                case GodStir.Death:
                {
                    int lying = map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse).OfType<Corpse>().Count(c => c.Spawned && c.InnerPawn?.RaceProps?.Humanlike == true);
                    return GodNearMath.CountStir(lying, 0f, 3f);
                }
                case GodStir.Sickness:
                {
                    int sick = map.mapPawns.FreeColonistsSpawned.Count(p => p.health?.hediffSet?.hediffs?.Any(h => h.Visible && h.def.lethalSeverity > 0f && h is HediffWithComps hw
                                                                                                                  && hw.TryGetComp<HediffComp_Immunizable>() != null) == true);
                    return GodNearMath.CountStir(sick, 0f, 3f);
                }
                case GodStir.Guests:
                {
                    int guests = map.mapPawns.AllPawnsSpawned.Count(x => x.RaceProps?.Humanlike == true && x.Faction != null && !x.Faction.IsPlayer && !x.HostileTo(Faction.OfPlayer)
                                                                        && !x.IsPrisoner && !x.Dead);
                    return GodNearMath.CountStir(guests, 0f, 4f);
                }
                case GodStir.Machines:
                {
                    int machines = 0, broken = 0;
                    foreach (var t in map.listerBuildings.allBuildingsColonist)
                    {
                        if (t.GetComp<CompPowerTrader>() != null) machines++;
                        if (t.GetComp<CompBreakdownable>()?.BrokenDown == true) broken++;
                    }
                    return Math.Max(GodNearMath.CountStir(machines, 10f, 40f), broken > 0 ? 0.5f : 0f);
                }
                case GodStir.Hoard:
                    return GodNearMath.CountStir(map.resourceCounter?.GetCount(ThingDefOf.Silver) ?? 0, 2000f, 10000f);
                case GodStir.Trade:
                {
                    bool trader = map.passingShipManager?.passingShips?.Count > 0
                                  || map.mapPawns.AllPawnsSpawned.Any(x => x.RaceProps?.Humanlike == true && x.Faction != null && !x.Faction.IsPlayer && Strangers.KindOf(x) == StrangerKind.Trader);
                    return trader ? 0.7f : 0f;
                }
                case GodStir.Herd:
                {
                    var wild = map.mapPawns.AllPawnsSpawned.Where(x => x.RaceProps?.Animal == true && x.Faction == null && !x.Dead).ToList();
                    if (wild.Any(x => x.def.defName == "Thrumbo" || x.def.defName.StartsWith("Thrumbo"))) return 1f;
                    int herd = wild.Count == 0 ? 0 : wild.GroupBy(x => x.def).Max(gr => gr.Count());
                    return GodNearMath.CountStir(herd, 7f, 14f) * 0.6f;
                }
                case GodStir.Cats:
                {
                    int cats = map.mapPawns.SpawnedColonyAnimals.Count(x => x.def.defName == "Cat" || x.def.defName.EndsWith("_Cat"));
                    return GodNearMath.CountStir(cats, 0f, 3f);
                }
                case GodStir.Desperate:
                    return GodVoid.Desperation(map) / 3f;
            }
            return 0f;
        }

        // ================= Relations =================

        /// <summary>Its relations' share: a quarrel it is in now (the world's drama), and the most stirred of the gods tied to it.</summary>
        private static float Relations(God g, Map map)
        {
            var world = GodWorld.Book;
            bool quarrel = world != null && world.Of("quarrel").Any(d => d != null && !d.done && (d.god == g.id || d.other == g.id));
            float tied = 0f;
            foreach (var t in Gods.TiesOf(g))
            {
                var other = Gods.ById(t.Other(g.id));
                if (other == null || other == g) continue;
                tied = Math.Max(tied, Situation(other, map));
            }
            return GodNearMath.Relations(quarrel, tied);
        }

        // ================= What the colony does =================

        /// <summary>The colony's share: its offerings to the god lately, harm lately to what it holds sacred, its book's entries of it lately, and its standing.</summary>
        private static float Colony(God g, Map map, int home, int now)
        {
            int offerings = 0, harms = 0, book = 0;
            var worship = WorshipBook.Instance;
            if (worship?.offerings != null)
                foreach (var o in worship.offerings)
                    if (o != null && o.god == g.id && o.home == home && now - o.chosen < GodNearMath.OfferingDays * GenDate.TicksPerDay)
                        offerings++;
            var gods = GodBook.Instance;
            if (gods?.acts != null)
                foreach (var a in gods.acts)
                    if (a != null && a.god == g.id && a.home == home && a.kind == GodActKind.Favour && a.favour <= GodNearMath.HarmFrom
                        && now - a.tick < GodNearMath.HarmDays * GenDate.TicksPerDay)
                        harms++;
            var lore = GodLore.Instance;
            if (lore?.entries != null)
            {
                var pages = new HashSet<string>(lore.names.Where(n => n != null && n.god == g.id).Select(n => n.name));
                foreach (var e in lore.entries)
                    if (e != null && e.page != null && pages.Contains(e.page) && now - e.tick < GodNearMath.BookDays * GenDate.TicksPerDay)
                        book++;
            }
            return GodNearMath.Colony(offerings, harms, book, Gods.FavourValue(g, map));
        }

        // ================= The dev tools =================

        /// <summary>Dev: a god as near as <paramref name="near"/> (below 0: as the game reads it again), until the game is left.</summary>
        public static void Force(God g, float near)
        {
            if (g == null) return;
            Fresh();
            if (near < 0f) forced.Remove(g.id);
            else forced[g.id] = Math.Min(1f, near);
            cache.Clear();
        }

        /// <summary>Dev: how near each god is at a home, and from what, for the log.</summary>
        public static string Report(Map home)
        {
            var map = home != null && Homes.IsHome(home) ? home : Homes.Seat;
            int key = Gods.HomeKey(map);
            int now = Now;
            var lines = new List<string>();
            foreach (var g in Gods.All.Where(x => x != null).OrderByDescending(x => Of(x, map)))
            {
                float n = Of(g, map);
                lines.Add("  " + g.name + ": " + n.ToString("0.00") + (forced.ContainsKey(g.id) ? " (dev)" : "") + " = situation " + Situation(g, map).ToString("0.00")
                          + ", relations " + Relations(g, map).ToString("0.00") + ", colony " + Colony(g, map, key, now).ToString("0.00") + "; plain " + GodNearMath.PlainChance(n).ToString("P0"));
            }
            return string.Join("\n", lines);
        }
    }
}
