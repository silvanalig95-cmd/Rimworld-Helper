using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// The gods' places with a logic behind them (the player, 24 November 2026: "maybe you should have some conditions
    /// fulfilled, for example, god actually known, believed in, or that someone wants to believe in a god ... Not that I ...
    /// can just again have an altar for each god at the same time for free within a minute"; docs/gods-altars-proposals.md,
    /// A1 to A8 and B5, B7). The rules are in GodPlacesMath; the dedication itself and the button in Shrines.
    /// <list type="bullet">
    /// <item>A1: a place only for a god someone of the colony holds, or has lately turned toward; the one who says the words holds it.</item>
    /// <item>A2: one place a god at a home, more as more hold it.</item>
    /// <item>A3: what suits (no shield, a grave only for a god of the dead, art only of good quality), and a place its believers
    /// say it likes (by water for the waters, by the fields for the harvest ...), by what the colony knows of the god.</item>
    /// <item>A6: upkeep: untended for a season, neglected; its believers unhappy, the first prayer there counting for less.</item>
    /// <item>A7: rival gods' places in one room trouble the believers of both.</item>
    /// <item>B5: the place's room (or what stands round it outdoors) makes it finer.</item>
    /// <item>B7: a keepsake of a god's moment kept at the god's place.</item>
    /// </list>
    /// </summary>
    public static class GodPlaces
    {
        private static int Now => Find.TickManager?.TicksGame ?? 0;

        // ================= Who holds a god =================

        /// <summary>Someone of the colony who holds a god: it is their god, or they lean toward it or more (not in dread, not a scoffer's doubt).</summary>
        public static bool HoldsFor(Pawn p, God g)
        {
            if (p == null || g == null || !Worship.Ours(p) || p.DevelopmentalStage.Baby()) return false;
            if (Worship.Scoffer(p) && !PawnGods.WonOver(p)) return false;
            if (Worship.GodOf(p) == g) return true;
            var w = PawnGods.WordOf(p, g);
            return w == BeliefWord.Leans || w == BeliefWord.Holds || w == BeliefWord.Devoted || w == BeliefWord.Fervent;
        }

        /// <summary>The colony's people at a home who hold a god.</summary>
        public static List<Pawn> Holders(Map map, God g) =>
            map == null || g == null ? new List<Pawn>() : map.mapPawns.FreeColonistsSpawned.Where(p => HoldsFor(p, g)).ToList();

        /// <summary>Someone who has lately turned toward a god (GodPlacesMath.Seeker): a moment, a dream or a vow raised their belief in it.</summary>
        public static bool Seeks(Pawn p, God g)
        {
            if (p == null || g == null || !Worship.Ours(p) || HoldsFor(p, g)) return false;
            var h = PawnGods.Of(p, false)?.Of(g.id);
            if (h == null || h.grew < 0) return false;
            return GodPlacesMath.Seeker(h.hold, (Now - h.grew) / (float)GenDate.TicksPerDay);
        }

        public static int Seekers(Map map, God g) => map == null || g == null ? 0 : map.mapPawns.FreeColonistsSpawned.Count(p => Seeks(p, g));

        // ================= What suits =================

        /// <summary>A grave, or a sarcophagus: a resting place.</summary>
        public static bool IsGrave(Thing t) => t is Building_Grave || t?.def?.thingClass != null && typeof(Building_Grave).IsAssignableFrom(t.def.thingClass);

        /// <summary>Something with "shrine" in its name that is no shrine (Vanilla Psycasts Expanded's shield generators).</summary>
        public static bool IsShield(Thing t) => t?.def?.defName != null && t.def.defName.IndexOf("Shield", StringComparison.OrdinalIgnoreCase) >= 0;

        /// <summary>A god of the dead: its domain or its second.</summary>
        public static bool DeadGod(God g) => g != null && (g.domain == GodDomain.Dead || g.second == GodDomain.Dead);

        public static PlaceSuit SuitOf(Thing t, God g)
        {
            int quality = t != null && t.TryGetQuality(out var q) ? (int)q : -1;
            return GodPlacesMath.Suits(IsShield(t), IsGrave(t), DeadGod(g), t?.def?.IsArt == true && t.def.ritualFocus == null, quality);
        }

        /// <summary>A god's places at a home, dedicated or being dedicated, but for <paramref name="except"/>.</summary>
        public static int PlacesOf(Map map, God g, Thing except = null)
        {
            var b = Worship.Book;
            if (b == null || map == null || g == null) return 0;
            return b.shrines.Count(s => s != null && s.Live && s.thing.Map == map && s.god == g.id && s.thing != except);
        }

        /// <summary>
        /// Why a place can't be given to a god now, in words (null when it can): nobody here holds it (A1), it has as many
        /// places as its believers can keep (A2), or the place doesn't suit it (A3).
        /// </summary>
        public static string WhyNot(Thing t, God g)
        {
            var map = t?.Map;
            if (map == null || g == null) return "";
            string god = Worship.Called(g);
            switch (SuitOf(t, g))
            {
                case PlaceSuit.No: return "CC_Wsh_Why_NotAPlace".Translate().Resolve();
                case PlaceSuit.GraveNotDead: return "CC_Wsh_Why_GraveNotDead".Translate(god.Named("GOD")).Resolve();
                case PlaceSuit.ArtTooPoor: return "CC_Wsh_Why_ArtTooPoor".Translate().Resolve();
            }
            int holders = Holders(map, g).Count;
            int seekers = holders > 0 ? 0 : Seekers(map, g);
            if (holders == 0 && seekers == 0) return "CC_Wsh_Why_NobodyHolds".Translate(god.Named("GOD")).Resolve();
            int places = PlacesOf(map, g, t);
            if (places >= GodPlacesMath.PlacesAllowed(holders, seekers))
            {
                int need = GodPlacesMath.HoldersFor(places);
                return need <= 0 ? "CC_Wsh_Why_MostPlaces".Translate(god.Named("GOD")).Resolve()
                    : "CC_Wsh_Why_MorePlaces".Translate(god.Named("GOD"), need.Named("COUNT")).Resolve();
            }
            return null;
        }

        /// <summary>Who may say the words: someone free who holds the god (the one who asked the council first, then the priest where the priest holds it, then the most devout).</summary>
        public static Pawn Dedicator(Map map, God g, Thing t, Pawn asked)
        {
            var who = new List<Pawn>();
            if (asked != null) who.Add(asked);
            var priest = Worship.Priest(map);
            if (priest != null) who.Add(priest);
            who.AddRange(Holders(map, g).OrderByDescending(p => PawnGods.HoldOf(p, g)));
            foreach (var p in who.Distinct())
            {
                if (p == null || p.Map != map || !HoldsFor(p, g) || p.DevelopmentalStage.Juvenile() || !WorshipJobs.Free(p, false)) continue;
                if (!p.CanReach(t, PathEndMode.Touch, Danger.Some)) continue;
                return p;
            }
            // A seeker's place, where nobody yet holds the god: the seeker says the words.
            if (Holders(map, g).Count == 0)
                foreach (var p in map.mapPawns.FreeColonistsSpawned.Where(x => Seeks(x, g)))
                    if (!p.DevelopmentalStage.Juvenile() && WorshipJobs.Free(p, false) && p.CanReach(t, PathEndMode.Touch, Danger.Some))
                        return p;
            return null;
        }

        // ================= A place its believers say it likes =================

        /// <summary>
        /// Whether a place is of the kind the god's believers say it likes (by its domain, as the colony knows the god): by water
        /// for the waters, by the fields for the harvest, by a fire for the hearth, away from lamps for the night and the void,
        /// by the graves for the dead, among trees for the wild and the hunt, under the open sky for the storm, by a bench for
        /// craft, by machines for the machine, by the sickbeds for healing, by weapons for war; null for a god with no such liking.
        /// </summary>
        public static bool? Fits(Thing t, God g)
        {
            if (t?.Map == null || g == null) return null;
            var d = g.domain;
            if (!Has(d)) d = g.second;
            if (!Has(d)) return null;
            try
            {
                return FitsDomain(t, d);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("worship: a place's fit", e);
                return null;
            }
        }

        private static bool Has(GodDomain d) => d != GodDomain.None && d != GodDomain.Way;

        private static bool FitsDomain(Thing t, GodDomain d)
        {
            var map = t.Map;
            var c = t.Position;
            switch (d)
            {
                case GodDomain.Waters:
                    return GenRadial.RadialCellsAround(c, 6f, true).Any(x => x.InBounds(map) && x.GetTerrain(map)?.IsWater == true);
                case GodDomain.Storm:
                    return !map.roofGrid.Roofed(c);
                case GodDomain.Harvest:
                    return GenRadial.RadialCellsAround(c, 8f, true).Any(x => x.InBounds(map) && map.zoneManager.ZoneAt(x) is Zone_Growing);
                default:
                    var near = GenRadial.RadialDistinctThingsAround(c, map, d == GodDomain.Night || d == GodDomain.Void ? 6f : 8f, true).Where(x => x != t).ToList();
                    switch (d)
                    {
                        case GodDomain.Hearth: return near.Any(x => x.TryGetComp<CompHeatPusher>() != null && x.def.building != null);
                        case GodDomain.Night:
                        case GodDomain.Void: return !near.Any(x => x.TryGetComp<CompGlower>() != null && x.def.building != null);
                        case GodDomain.Dead: return near.Any(x => x is Building_Grave);
                        case GodDomain.Wild:
                        case GodDomain.Hunt: return near.Count(x => x is Plant p && p.def.plant?.IsTree == true) >= 3;
                        case GodDomain.Craft: return near.Any(x => x is Building_WorkTable);
                        case GodDomain.Machine: return near.Any(x => x is Building && x.TryGetComp<CompPowerTrader>() != null);
                        case GodDomain.Healing: return near.Any(x => x is Building_Bed bed && bed.Medical);
                        case GodDomain.War: return near.Any(x => x is Building_TurretGun || x.def.IsWeapon);
                        default: return false;
                    }
            }
        }

        /// <summary>The factor of a place's fit for prayers, belief and rites there (GodPlacesMath.FitFactor).</summary>
        public static float FitOf(Thing t, God g) => GodPlacesMath.FitFactor(Fits(t, g));

        /// <summary>What the god's believers say it likes in a place, in a few words ("by water"), or null.</summary>
        public static string FitWords(God g)
        {
            if (g == null) return null;
            var d = Has(g.domain) ? g.domain : g.second;
            if (!Has(d)) return null;
            string key = "CC_Wsh_Fit_" + d;
            return key.CanTranslate() ? key.Translate().Resolve() : null;
        }

        // ================= Upkeep =================

        /// <summary>Days since someone prayed or left something at a dedicated place (or since it was dedicated).</summary>
        public static float DaysUntended(ShrineRecord r)
        {
            // Not yet seen since upkeep was kept (an older save's place): untended from the hour it is first seen (Refresh).
            if (r == null || r.tended < 0) return 0f;
            return (Now - Math.Max(r.tended, r.since)) / (float)GenDate.TicksPerDay;
        }

        public static bool Neglected(ShrineRecord r) => r != null && !r.pending && GodPlacesMath.Neglected(DaysUntended(r));

        public static bool Neglected(Thing t) => Neglected(Worship.Book?.ShrineOf(t));

        /// <summary>Someone prayed, left something or held a rite at a god's place: it is tended.</summary>
        public static void Tend(Thing t)
        {
            var r = Worship.Book?.ShrineOf(t);
            if (r != null && !r.pending) r.tended = Now;
        }

        // ================= The place's room =================

        /// <summary>How fine a place's surroundings are, 0 to 1 (GodPlacesMath.RoomFine): its room, or what stands round it outdoors, and what its believers say it likes.</summary>
        public static float RoomFine(Thing t, God g)
        {
            if (t?.Map == null) return 0f;
            try
            {
                var room = t.GetRoom();
                bool indoors = room != null && !room.PsychologicallyOutdoors && room.ProperRoom;
                float imp = indoors ? room.GetStat(RoomStatDefOf.Impressiveness) : 0f;
                int around = 0;
                if (!indoors)
                    around = GenRadial.RadialDistinctThingsAround(t.Position, t.Map, 4f, true)
                        .Count(x => x != t && x is Building b && (b.TryGetComp<CompGlower>() != null || b.def.IsArt || b is Building_PlantGrower || b.def.building?.isSittable == true));
                int liked = Fits(t, g) == true ? 1 : 0;
                if (Keepsake(t, g) != null) liked++;
                return GodPlacesMath.RoomFine(indoors, imp, around, liked);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("worship: a place's room", e);
                return 0f;
            }
        }

        // ================= Keepsakes =================

        /// <summary>A keepsake of one of the god's moments lying at its place (B7), or null.</summary>
        public static Thing Keepsake(Thing place, God g)
        {
            if (place?.Map == null || g == null) return null;
            foreach (var x in GenRadial.RadialDistinctThingsAround(place.Position, place.Map, GodPlacesMath.KeepsakeNear, true))
            {
                var k = x.TryGetComp<CompGodKeepsake>();
                if (k != null && k.god == g.id) return x;
            }
            return null;
        }

        /// <summary>
        /// A god's moment has left something on the land (a find, a gift in kind, a rare gift): now and then a keepsake of it
        /// lies there too, a small thing to remember it by (Rimfolk_GodKeepsake), marked with the god the colony took it for.
        /// </summary>
        public static void LeaveKeepsake(God g, Map map, IntVec3 cell, string what)
        {
            if (g == null || map == null || !cell.IsValid || !Worship.On) return;
            var def = DefDatabase<ThingDef>.GetNamedSilentFail("Rimfolk_GodKeepsake");
            if (def == null || !Rand.Chance(GodPlacesMath.KeepsakeChance) || !Interplay.Ready("god-keepsake|" + Gods.HomeKey(map), GodPlacesMath.KeepsakeGapDays)) return;
            try
            {
                var t = ThingMaker.MakeThing(def);
                var k = t.TryGetComp<CompGodKeepsake>();
                if (k != null)
                {
                    k.god = g.id;
                    k.godName = Worship.Called(g);
                    k.what = what;
                    k.at = Now;
                }
                GenPlace.TryPlaceThing(t, cell, map, ThingPlaceMode.Near);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("worship: a keepsake left", e);
            }
        }

        /// <summary>
        /// The hour: a keepsake lying loose at a home whose god has a place there is carried to it by someone who holds the
        /// god, in their free time, and set down beside it (one at a time).
        /// </summary>
        private static void CarryKeepsakes(Map map)
        {
            var def = DefDatabase<ThingDef>.GetNamedSilentFail("Rimfolk_GodKeepsake");
            if (def == null) return;
            foreach (var t in map.listerThings.ThingsOfDef(def).ToList())
            {
                var k = t.TryGetComp<CompGodKeepsake>();
                var g = k == null ? null : Gods.ById(k.god);
                if (g == null || !t.Spawned) continue;
                var places = Shrines.Of(map, g).Select(s => s.thing).ToList();
                if (places.Count == 0) continue;
                if (places.Any(x => t.Position.InHorDistOf(x.Position, GodPlacesMath.KeepsakeNear)))
                {
                    // Kept there: left lying, not hauled off to the stores.
                    if (!t.IsForbidden(Faction.OfPlayer)) t.SetForbidden(true, false);
                    continue;
                }
                var place = places.OrderByDescending(Shrines.IsTemple).First();
                if (map.mapPawns.FreeColonistsSpawned.Any(p => p.CurJob?.targetA.Thing == t)) return;
                foreach (var p in Holders(map, g).OrderByDescending(x => PawnGods.HoldOf(x, g)))
                {
                    if (!WorshipJobs.Free(p) || !p.CanReserveAndReach(t, PathEndMode.ClosestTouch, Danger.Some)) continue;
                    var cell = Offerings.DropCell(place, p);
                    if (!cell.IsValid) return;
                    var job = JobMaker.MakeJob(JobDefOf.HaulToCell, t, cell);
                    job.count = 1;
                    job.haulMode = HaulMode.ToCellNonStorage;
                    if (!WorshipJobs.Start(p, job)) continue;
                    Worship.Say(p, "Rimfolk_Pool_Worship_Keepsake", g);
                    return;
                }
            }
        }

        // ================= Rivals side by side =================

        /// <summary>By home: the gods whose place shares a room with a rival god's place (A7), and those with a neglected place (A6); refreshed each hour.</summary>
        private static readonly Dictionary<int, HashSet<int>> rivalsSide = new Dictionary<int, HashSet<int>>(), neglected = new Dictionary<int, HashSet<int>>();

        private static Game cacheGame;

        private static HashSet<int> Of(Dictionary<int, HashSet<int>> d, Map map)
        {
            if (Current.Game != cacheGame)
            {
                rivalsSide.Clear();
                neglected.Clear();
                cacheGame = Current.Game;
            }
            if (map == null) return null;
            return d.TryGetValue(map.uniqueID, out var s) ? s : null;
        }

        /// <summary>Two places in one room (or, outdoors, within six cells of each other).</summary>
        private static bool SideBySide(Thing a, Thing b)
        {
            if (a?.Map == null || a.Map != b?.Map) return false;
            var ra = a.GetRoom();
            var rb = b.GetRoom();
            if (ra != null && ra == rb && !ra.PsychologicallyOutdoors) return true;
            return a.Position.InHorDistOf(b.Position, 6f);
        }

        private static void Refresh(WorshipBook b, Map map)
        {
            Of(rivalsSide, map);
            var live = b.shrines.Where(s => s != null && !s.pending && s.Live && s.thing.Map == map).ToList();
            var rivals = new HashSet<int>();
            for (int i = 0; i < live.Count; i++)
            for (int j = i + 1; j < live.Count; j++)
            {
                if (live[i].god == live[j].god) continue;
                var gi = Gods.ById(live[i].god);
                var gj = Gods.ById(live[j].god);
                if (gi == null || gj == null || !Gods.Rivals(gi).Contains(gj) && !Gods.Rivals(gj).Contains(gi)) continue;
                if (!SideBySide(live[i].thing, live[j].thing)) continue;
                rivals.Add(gi.id);
                rivals.Add(gj.id);
            }
            rivalsSide[map.uniqueID] = rivals;
            var cold = new HashSet<int>();
            foreach (var s in live)
            {
                // A place from before upkeep was kept (an older save) starts its season now, so nothing is neglected the day it loads.
                if (s.tended < 0) s.tended = Now;
                if (Neglected(s)) cold.Add(s.god);
            }
            neglected[map.uniqueID] = cold;
        }

        /// <summary>A god someone holds has a place at their home that shares its room with a rival god's place (A7), or null.</summary>
        public static God RivalSideFor(Pawn p) => FirstHeld(p, Of(rivalsSide, p?.MapHeld));

        /// <summary>A god someone holds has a neglected place at their home (A6), or null.</summary>
        public static God NeglectedFor(Pawn p) => FirstHeld(p, Of(neglected, p?.MapHeld));

        private static God FirstHeld(Pawn p, HashSet<int> gods)
        {
            if (p == null || gods == null || gods.Count == 0) return null;
            foreach (int id in gods)
            {
                var g = Gods.ById(id);
                if (g != null && HoldsFor(p, g)) return g;
            }
            return null;
        }

        // ================= Behind someone's portrait =================

        /// <summary>
        /// The domain of the god whose place someone is at, for the backdrop behind their portrait (C1): praying, keeping a
        /// rite or swearing at a god's place; else standing indoors in the room of one (its temple, the god's first); else
        /// "" (a god with no domain too).
        /// </summary>
        public static string DomainAt(Pawn p)
        {
            var b = Worship.Book;
            var map = p?.MapHeld;
            if (b == null || map == null || b.shrines.Count == 0) return "";
            God g = null;
            var target = p.CurJob?.targetA.Thing;
            if (target != null && p.CurJob.def.driverClass != null && typeof(JobDriver_WorshipAt).IsAssignableFrom(p.CurJob.def.driverClass)) g = Shrines.GodAt(target);
            if (g == null)
            {
                var room = p.PositionHeld.GetRoom(map);
                if (room != null && !room.PsychologicallyOutdoors)
                {
                    var here = b.shrines.Where(s => s != null && !s.pending && s.Live && s.thing.Map == map && s.thing.GetRoom() == room).Select(s => s.thing)
                        .OrderByDescending(Shrines.IsTemple).FirstOrDefault();
                    if (here != null) g = Shrines.GodAt(here);
                }
            }
            if (g == null) return "";
            var d = g.domain != GodDomain.None ? g.domain : g.second;
            return d == GodDomain.None ? "" : d.ToString().ToLowerInvariant();
        }

        // ================= The hour and the day =================

        public static void Hourly(WorshipBook b, Map map, int now)
        {
            Refresh(b, map);
            Worship.Safe("a keepsake carried", () => CarryKeepsakes(map));
            Worship.Safe("the week's service", () => GodRites.Services(b, map, now));
            Worship.Safe("pilgrims at the temple", () => GodRites.PilgrimsHourly(b, map, now));
        }

        public static void Daily(WorshipBook b, Map map, int now)
        {
            // A place gone untended for a season: said once, its believers unhappy until it is tended again (ThoughtWorker_GodPlaceNeglected).
            foreach (var s in b.shrines.Where(x => x != null && !x.pending && x.Live && x.thing.Map == map).ToList())
            {
                var g = Gods.ById(s.god);
                if (g == null || !Neglected(s) || !Interplay.Ready("godplace-neglect|" + s.thing.thingIDNumber, GodPlacesMath.NeglectDays)) continue;
                Tell.Minor(NewsKind.Colony, Importance.Notable, map, "CC_Wsh_Neglected".Translate(s.thing.LabelShort.Named("PLACE"), Worship.Called(g).Named("GOD")).Resolve(), s.thing);
            }
            Worship.Safe("a place asked for", () => GodPlacesCouncil.AskedDaily(b, map, now));
            Worship.Safe("pilgrims sent for", () => GodRites.PilgrimsDaily(b, map, now));
        }

        // ================= Taken down =================

        /// <summary>
        /// The colony tears down a god's place (A5): the god takes it the harder the longer the place was its own. A place
        /// burned or broken in a raid is no slight (its believers grieve it: PawnGodsActs.ShrinesLost).
        /// </summary>
        public static void TornDown(Thing t)
        {
            var b = Worship.Book;
            var r = b?.ShrineOf(t);
            if (r == null || r.pending) return;
            var g = Gods.ById(r.god);
            if (g == null) return;
            float days = r.since < 0 ? 0f : (Now - r.since) / (float)GenDate.TicksPerDay;
            Worship.Favour(g, t.MapHeld, GodPlacesMath.UndedicateFavour(days), "CC_Wsh_Why_TornDown".Translate(t.LabelShort.Named("PLACE")).Resolve(), FavourSource.Neglect,
                GodOffence.Neglect);
        }
    }

    /// <summary>A god's place torn down by the colony (deconstructed): the god takes it ill, the more the longer it was its own (GodPlaces.TornDown).</summary>
    [HarmonyPatch(typeof(Building), nameof(Building.Destroy))]
    public static class Patch_GodPlaceTornDown
    {
        public static void Prefix(Building __instance, DestroyMode mode)
        {
            if (!RimfolkPatches.On || mode != DestroyMode.Deconstruct || __instance == null || !__instance.Spawned) return;
            try
            {
                if (Worship.Book?.ShrineOf(__instance) != null) GodPlaces.TornDown(__instance);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("worship: a place torn down", e);
            }
        }
    }

    /// <summary>A god's place left untended for a season: those who hold its god are unhappy about it (A6).</summary>
    public class ThoughtWorker_GodPlaceNeglected : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            if (!Worship.On || p == null || !p.IsColonist || !People.HasInnerLife(p)) return false;
            return GodPlaces.NeglectedFor(p) != null;
        }
    }

    /// <summary>A god's place sharing its room with a rival god's: the believers of both are troubled (A7).</summary>
    public class ThoughtWorker_GodPlaceRivals : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            if (!Worship.On || p == null || !p.IsColonist || !People.HasInnerLife(p)) return false;
            return GodPlaces.RivalSideFor(p) != null;
        }
    }

    /// <summary>A keepsake of a god's moment: which god the colony took it for, what it was, when (B7).</summary>
    public class CompProperties_GodKeepsake : CompProperties
    {
        public CompProperties_GodKeepsake() => compClass = typeof(CompGodKeepsake);
    }

    public class CompGodKeepsake : ThingComp
    {
        public int god = -1, at = -1;
        public string godName, what;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref god, "god", -1);
            Scribe_Values.Look(ref at, "at", -1);
            Scribe_Values.Look(ref godName, "godName");
            Scribe_Values.Look(ref what, "what");
        }

        public override string CompInspectStringExtra()
        {
            if (godName.NullOrEmpty()) return null;
            string line = "CC_Wsh_Keepsake_Of".Translate(godName.Named("GOD")).Resolve();
            if (!what.NullOrEmpty()) line += "\n" + "CC_Wsh_Keepsake_What".Translate(what.Named("WHAT")).Resolve();
            if (at >= 0) line += "\n" + "CC_Wsh_Keepsake_When".Translate(GenDate.DateFullStringAt(GenDate.TickGameToAbs(at), Find.WorldGrid.LongLatOf(parent.MapHeld?.Tile ?? 0)).Named("DATE")).Resolve();
            return line;
        }

        public override bool AllowStackWith(Thing other) => false;
    }
}
