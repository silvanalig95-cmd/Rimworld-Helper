using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    [DefOf]
    public static class IdleDefOf
    {
        public static JobDef Rimfolk_IdleChat;
        public static JobDef Rimfolk_IdleCompany;
        public static JobDef Rimfolk_IdleFire;
        public static JobDef Rimfolk_IdleWarm;
        public static JobDef Rimfolk_IdleSeat;
        public static JobDef Rimfolk_IdleShelter;
        public static JobDef Rimfolk_IdleSnack;
        public static JobDef Rimfolk_IdleNap;
        public static JobDef Rimfolk_IdleGame;
        public static JobDef Rimfolk_IdleMusic;
        public static JobDef Rimfolk_IdleStars;
        public static JobDef Rimfolk_IdleArt;
        public static JobDef Rimfolk_IdlePray;
        public static JobDef Rimfolk_IdleAnimal;
        public static JobDef Rimfolk_IdlePlay;
        public static JobDef Rimfolk_IdleOwnDrug;
        public static JobDef Rimfolk_IdlePipe;
        public static JobDef Rimfolk_IdleCarve;
        public static JobDef Rimfolk_IdleRead;
        public static JobDef Rimfolk_IdleWalk;
        public static JobDef Rimfolk_IdlePet;
        public static JobDef Rimfolk_IdleWrite;
        public static JobDef Rimfolk_IdleTrain;
        public static JobDef Rimfolk_IdleArchery;
        public static JobDef Rimfolk_IdleFish;
        public static JobDef Rimfolk_IdleMend;
        public static JobDef Rimfolk_IdleSharpen;
        public static JobDef Rimfolk_IdleCards;
        public static JobDef Rimfolk_IdleSing;
        public static JobDef Rimfolk_IdleSleep;

        static IdleDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(IdleDefOf));
        }
    }

    /// <summary>
    /// A visitor's idle hours filled with things of their own (the player's requests, 14 November 2026: "guests should spend
    /// more of their time doing things. i don't want them to generate value for me, but it just looks a bit weird when they
    /// spend most of their day just standing and walking around"; 15 November 2026: "they could read books, take walks,
    /// interact with my pets, write letters, train, fish, work on and use items themselves etc.", and "they generally just
    /// chill around the same room"). Where a visitor's duty would have them wander about their spot or stand about for want
    /// of anything to do (Patch_VisitorsIdle, the same way in as the market's), the game's visitors and a caravan's people,
    /// Rimfolk's own visits between their duties, envoys, and Hospitality's guests once its own recreation has had its turn,
    /// mostly do something of their own instead: talk with one of their own or keep one of the colony's people company, sit by
    /// a fire or on a bench, warm their hands in the cold, shelter from the rain, eat from their own pack, nap in the shade,
    /// play the colony's games and instruments or look through its telescope while nobody of the colony wants them, look at
    /// its art, pray at a shrine of their god, see to their own animals, run about at play (the children), have a smoke or a
    /// drink of their own, smoke a pipe or whittle; read a book of their own or one of the colony's where it lies, walk round
    /// the colony to a garden, a grave or a view, pet the colony's dogs and cats or throw a stick for a dog, write a letter
    /// or their journal at a table, train at the colony's targets and dummies or spar with their shadow, practise with their
    /// own bow at the colony's archery target, fish at the water's edge, mend their clothes, see to their weapon, play cards,
    /// dice or stones with one of their own, sing to themselves, and sleep on the ground on a tired night. What fits whom by
    /// their people's age and ways, the hour and the weather is IdleHoursMath's; they spread over the colony's rooms and
    /// grounds (IdleHoursMath.PlaceScore), Hospitality's guests within their guest area when one is set. Nothing is made,
    /// hauled or cleaned for the colony, nothing of its stores is used, a book read is put back where it lay, and nothing is
    /// reserved: one of the colony's people who wants a game, a seat or a place finds it free, and the visitor gets up as soon
    /// as they set out for it. Nothing is saved but the game's own job.
    /// </summary>
    public static class IdleHours
    {
        public static bool On => RimfolkPatches.On && (ColonyCouncilMod.Settings?.visitorsIdle ?? true);

        public const string Pool = "Rimfolk_Pool_VisitorIdle";

        private static int Now => Find.TickManager?.TicksGame ?? 0;

        // ---------------- Per session ----------------

        private static Game game;
        /// <summary>When each visitor (thingIDNumber) looks for something to do next.</summary>
        private static readonly Dictionary<int, int> nextLook = new Dictionary<int, int>();
        /// <summary>A joy thing a visitor gave up to one of the colony's people (thingIDNumber of the visitor to the thing's and the tick it may be tried again).</summary>
        private static readonly Dictionary<int, (int thing, int until)> gaveWay = new Dictionary<int, (int, int)>();
        /// <summary>When each visitor, and each map, last said a line.</summary>
        private static readonly Dictionary<int, int> saidBy = new Dictionary<int, int>(), saidOn = new Dictionary<int, int>();
        /// <summary>Visitors at a stint now: so two of them don't go for one place.</summary>
        private static readonly List<Pawn> busy = new List<Pawn>();
        private static readonly Dictionary<int, Places> places = new Dictionary<int, Places>();

        /// <summary>For the log: each visitor's ticks at stints and left standing about this session, the stint they are at since when, and since when they stand about.</summary>
        private sealed class Tally
        {
            public int stint, idle, stints, startedAt = -1, idleSince = -1;
        }

        private static readonly Dictionary<int, Tally> tallies = new Dictionary<int, Tally>();

        private static void Fresh()
        {
            if (game == Current.Game) return;
            game = Current.Game;
            nextLook.Clear();
            gaveWay.Clear();
            saidBy.Clear();
            saidOn.Clear();
            busy.Clear();
            places.Clear();
            tallies.Clear();
        }

        private static Tally TallyOf(Pawn p)
        {
            if (tallies.TryGetValue(p.thingIDNumber, out var t)) return t;
            if (tallies.Count > 400) tallies.Clear();
            return tallies[p.thingIDNumber] = new Tally();
        }

        // ---------------- The colony's things, by map, looked at once an hour ----------------

        /// <summary>One of the colony's joy things as the game's joy giver for it has it.</summary>
        public sealed class JoyThing
        {
            public JoyGiverDef giver;
            public JobDef job;
            public JoyShape shape;
            public JoyThrow toss;
            public int age;
            public bool telescope, desireSit;
            /// <summary>Played as music (an instrument, a piano), or watched and listened to (a screen, a radio).</summary>
            public bool music, watch;
            /// <summary>One of the game's instruments, which plays its own music while someone is at it.</summary>
            public bool instrument;
            public int participants = 1;
        }

        public sealed class Places
        {
            public int tick;
            public readonly List<Building> fires = new List<Building>(), seats = new List<Building>(), games = new List<Building>(), music = new List<Building>(),
                stars = new List<Building>(), art = new List<Building>(), altars = new List<Building>();
            /// <summary>The colony's books lying about (on a shelf, a table, the floor) and its bookcases with books in them.</summary>
            public readonly List<Thing> books = new List<Thing>();
            public readonly List<Building> bookcases = new List<Building>();
            /// <summary>The colony's tables to eat at (not a work bench), to write a letter at.</summary>
            public readonly List<Building> tables = new List<Building>();
            /// <summary>The colony's training targets and dummies, and its archery targets (IdleHoursMath.SparTargets, ArcheryTargets).</summary>
            public readonly List<Building> sparTargets = new List<Building>(), archeryTargets = new List<Building>();
            /// <summary>Somewhere to walk to: a grave with someone in it, a garden (flowers in a pot or a bed), a view (the water's edge, the colony's edge looking out), and the colony's paths on the way.</summary>
            public readonly List<Building> graves = new List<Building>();
            public readonly List<IntVec3> gardens = new List<IntVec3>(), paths = new List<IntVec3>();
            public readonly List<(IntVec3 stand, IntVec3 face)> views = new List<(IntVec3, IntVec3)>();
            /// <summary>Places at the water's edge with fish in the water, off the colony's own fishing spots.</summary>
            public readonly List<(IntVec3 stand, IntVec3 water)> fishSpots = new List<(IntVec3, IntVec3)>();
            /// <summary>
            /// Cells where someone works at one of the colony's buildings (by the map's cell index): a chair there is a work
            /// chair, not a seat to take, and nobody idles on a cook's or a crafter's spot.
            /// </summary>
            public readonly HashSet<int> workCells = new HashSet<int>();
        }

        private static Dictionary<ThingDef, JoyThing> joyThings;

        /// <summary>
        /// The colony's joy things of every loaded mod, by the game's joy givers: every building a joy giver for building use
        /// lists (the game's horseshoes, chess, cards, screens, telescope and instruments; Vanilla Furniture Expanded's arcade,
        /// roulette and computers; the Settlers' faro and knife game; Odyssey's and the quests' tables...), with how it is used
        /// and its age; baths, pools, shooting and the like left out (IdleHoursMath.Excluded).
        /// </summary>
        private static Dictionary<ThingDef, JoyThing> JoyThings
        {
            get
            {
                if (joyThings != null) return joyThings;
                var map = new Dictionary<ThingDef, JoyThing>();
                try
                {
                    foreach (var g in DefDatabase<JoyGiverDef>.AllDefsListForReading)
                    {
                        if (g?.giverClass == null || g.thingDefs.NullOrEmpty() || !typeof(JoyGiver_InteractBuilding).IsAssignableFrom(g.giverClass)) continue;
                        foreach (var def in g.thingDefs)
                        {
                            if (def == null || def.category != ThingCategory.Building || map.ContainsKey(def)) continue;
                            if (IdleHoursMath.Excluded(g.defName, g.joyKind?.defName, def.defName)) continue;
                            bool instrument = def.thingClass != null && typeof(Building_MusicalInstrument).IsAssignableFrom(def.thingClass);
                            bool watch = typeof(JoyGiver_WatchBuilding).IsAssignableFrom(g.giverClass);
                            map[def] = new JoyThing
                            {
                                giver = g,
                                job = g.jobDef,
                                shape = IdleHoursMath.ShapeOf(instrument, watch,
                                    typeof(JoyGiver_InteractBuildingSitAdjacent).IsAssignableFrom(g.giverClass) || typeof(JoyGiver_PlayBilliards).IsAssignableFrom(g.giverClass),
                                    typeof(JoyGiver_InteractBuildingInteractionCell).IsAssignableFrom(g.giverClass), def.hasInteractionCell),
                                toss = IdleHoursMath.ThrowOf(g.jobDef?.driverClass?.FullName),
                                age = AgeOfThing(def),
                                telescope = IdleHoursMath.IsTelescope(g.joyKind?.defName, def.defName),
                                music = IdleHoursMath.IsMusic(instrument, g.joyKind?.defName, watch),
                                watch = watch,
                                instrument = instrument,
                                desireSit = g.desireSit,
                                participants = Math.Max(1, g.jobDef?.joyMaxParticipants ?? 1)
                            };
                        }
                    }
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("visitors' idle hours: the joy things", e);
                }
                joyThings = map;
                return map;
            }
        }

        /// <summary>The joy thing a building is, or null.</summary>
        public static JoyThing JoyOf(ThingDef def) => def != null && JoyThings.TryGetValue(def, out var j) ? j : null;

        /// <summary>The age of one of the colony's things: its own, its research's, its parts' and its power (IdleHoursMath.ThingAge).</summary>
        public static int AgeOfThing(ThingDef def)
        {
            if (def == null) return 0;
            int research = 0;
            if (def.researchPrerequisites != null)
                foreach (var r in def.researchPrerequisites)
                    if (r != null) research = Math.Max(research, (int)r.techLevel);
            bool industrial = false, spacer = false;
            if (def.costList != null)
                foreach (var c in def.costList)
                {
                    if (c?.thingDef == null) continue;
                    if (c.thingDef == ThingDefOf.ComponentIndustrial) industrial = true;
                    if (c.thingDef == ThingDefOf.ComponentSpacer || c.thingDef == ThingDefOf.Plasteel) spacer = true;
                }
            return IdleHoursMath.ThingAge((int)def.techLevel, research, industrial, spacer, def.GetCompProperties<CompProperties_Power>() != null);
        }

        private static readonly string[] NotSeats = { "Toilet", "Latrine", "Commode", "Throne", "Bath", "Saddle", "Pew" };

        private static Places PlacesOf(Map map)
        {
            int now = Now;
            if (places.TryGetValue(map.uniqueID, out var pl) && now >= pl.tick && now - pl.tick < GenDate.TicksPerHour) return pl;
            pl = new Places { tick = now };
            try
            {
                var joy = JoyThings;
                foreach (var b in map.listerBuildings.allBuildingsColonist)
                {
                    if (b == null || !b.Spawned || b.def == null || b is Frame) continue;
                    var def = b.def;
                    if (def.hasInteractionCell) pl.workCells.Add(map.cellIndices.CellToIndex(b.InteractionCell));
                    if (joy.TryGetValue(def, out var j))
                    {
                        if (j.telescope) pl.stars.Add(b);
                        else if (j.music) pl.music.Add(b);
                        else pl.games.Add(b);
                        continue;
                    }
                    if (IdleHoursMath.SitByFire(Sauna.IsFire(def), def.defName, def.GetCompProperties<CompProperties_Power>() != null, def.IsWorkTable, def.size.x * def.size.z))
                        pl.fires.Add(b);
                    else if (def.building != null && def.building.isSittable && !(b is Building_Bed)
                             && !NotSeats.Any(n => def.defName.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0)) pl.seats.Add(b);
                    // Art as the game's own looking at art has it: a sculpture, or anything whose art is there to be enjoyed.
                    else if (b.TryGetComp<CompArt>() is CompArt art && art.Active && art.Props.canBeEnjoyedAsArt && !(b is Building_Bed)) pl.art.Add(b);
                    if (def.ritualFocus != null) pl.altars.Add(b);
                    if (b is Building_Bookcase bc && bc.HeldBooks.Count > 0) pl.bookcases.Add(b);
                    else if (def.surfaceType == SurfaceType.Eat && !def.IsWorkTable && !(def.building?.isSittable ?? false)) pl.tables.Add(b);
                    if (IdleHoursMath.IsSparTarget(def.defName)) pl.sparTargets.Add(b);
                    else if (IdleHoursMath.IsArcheryTarget(def.defName)) pl.archeryTargets.Add(b);
                    if (b is Building_Grave grave && grave.HasCorpse) pl.graves.Add(b);
                    else if (b is Building_PlantGrower pot && !def.IsWorkTable && pot.PlantsOnMe.Any(Flowers)) pl.gardens.Add(b.Position);
                }
                // A work chair is no seat to take.
                pl.seats.RemoveAll(s => pl.workCells.Contains(map.cellIndices.CellToIndex(s.Position)));
                foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.Book))
                    if (t is Book book && book.Spawned && ReadableBook(book))
                        pl.books.Add(t);
                Grounds(map, pl);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("visitors' idle hours: the colony's things", e);
            }
            places[map.uniqueID] = pl;
            return pl;
        }

        /// <summary>A plant grown for its looks (a rose, a daylily), not a crop.</summary>
        private static bool Flowers(Plant plant) => plant?.def?.plant != null && plant.def.plant.purpose == PlantPurpose.Beauty && plant.Growth > 0.3f;

        /// <summary>A book visitors may read idly: not a dark tome that can break the reader's mind.</summary>
        private static bool ReadableBook(Book b)
        {
            try
            {
                return IdleHoursMath.BookOk(b.IsReadable, b.MentalBreakChancePerHour);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// The colony's grounds, sampled across the home area and a little round it: flower beds, the colony's paths out of
        /// doors (its laid floors), the views (the water's edge, the edge of the home area looking out) and the places to
        /// fish from (with Odyssey the water's own fish stock, Vanilla Fishing Expanded's open water on its own; never in one
        /// of the colony's fishing zones).
        /// </summary>
        private static void Grounds(Map map, Places pl)
        {
            var home = map.areaManager?.Home;
            if (home == null || home.TrueCount == 0) return;
            int minX = int.MaxValue, minZ = int.MaxValue, maxX = int.MinValue, maxZ = int.MinValue;
            foreach (var c in home.ActiveCells)
            {
                if (c.x < minX) minX = c.x;
                if (c.z < minZ) minZ = c.z;
                if (c.x > maxX) maxX = c.x;
                if (c.z > maxZ) maxZ = c.z;
            }
            const int margin = 20;
            minX = Math.Max(1, minX - margin);
            minZ = Math.Max(1, minZ - margin);
            maxX = Math.Min(map.Size.x - 2, maxX + margin);
            maxZ = Math.Min(map.Size.z - 2, maxZ + margin);
            bool fishing = CampLand.Odyssey || ModsConfig.IsActive("VanillaExpanded.VCEF");
            foreach (var z in map.zoneManager.AllZones)
            {
                if (!(z is Zone_Growing g) || g.cells.Count == 0) continue;
                var plant = g.GetPlantDefToGrow();
                if (plant?.plant == null || plant.plant.purpose != PlantPurpose.Beauty) continue;
                for (int i = 0; i < g.cells.Count && i < 40; i += 8) pl.gardens.Add(g.cells[i]);
            }
            for (int x = minX; x <= maxX; x += 3)
                for (int zz = minZ; zz <= maxZ; zz += 3)
                {
                    var c = new IntVec3(x, 0, zz);
                    if (c.Fogged(map)) continue;
                    var t = c.GetTerrain(map);
                    if (t == null) continue;
                    if (t.IsWater)
                    {
                        var land = Shore(map, c);
                        if (!land.IsValid) continue;
                        if (pl.views.Count < 40 && Rand.Chance(0.5f)) pl.views.Add((land, c));
                        if (fishing && pl.fishSpots.Count < 60 && CampLand.Fishable(map, c) && !(map.zoneManager.ZoneAt(c) is Zone_Fishing) && !(map.zoneManager.ZoneAt(land) is Zone_Fishing))
                            pl.fishSpots.Add((land, c));
                        continue;
                    }
                    if (!home[c] || !c.Standable(map) || !c.UsesOutdoorTemperature(map)) continue;
                    if (t.layerable && pl.paths.Count < 120) pl.paths.Add(c);
                    if (pl.views.Count < 60)
                        foreach (var dir in GenAdj.CardinalDirections)
                        {
                            var out4 = c + dir * 4;
                            if (out4.InBounds(map) && !home[out4] && out4.UsesOutdoorTemperature(map) && !out4.Roofed(map))
                            {
                                pl.views.Add((c, out4));
                                break;
                            }
                        }
                }
        }

        /// <summary>A dry, standable cell beside the water to stand on (and fish from), or Invalid.</summary>
        private static IntVec3 Shore(Map map, IntVec3 water)
        {
            foreach (var dir in GenAdj.CardinalDirections)
            {
                var s = water + dir;
                if (!s.InBounds(map) || !s.Standable(map) || s.Fogged(map) || (s.GetTerrain(map)?.IsWater ?? true) || s.GetEdifice(map) != null) continue;
                return s;
            }
            return IntVec3.Invalid;
        }

        // ---------------- Who ----------------

        /// <summary>The kind of group a visitor is with: the game's visitors and caravans, Rimfolk's visits and envoys, Hospitality's guests; None for anyone else (raiders, camps, feasts, quests, the market's own).</summary>
        public static IdleGroup GroupOf(Pawn p, Lord lord)
        {
            var job = lord?.LordJob;
            if (job == null || lord.faction == null || lord.faction.IsPlayer) return IdleGroup.None;
            string ns = job.GetType().Namespace ?? "";
            if (ns == "Hospitality") return IdleGroup.Hospitality;
            if (ns.StartsWith("RimMarket")) return IdleGroup.None;
            if (job is LordJob_TradeWithColony) return IdleGroup.Caravan;
            if (job is LordJob_VisitColony) return IdleGroup.GameVisit;
            if (job is LordJob_RimfolkVisit) return IdleGroup.RimfolkVisit;
            if (job is LordJob_EnvoyStay) return IdleGroup.Envoys;
            return IdleGroup.None;
        }

        /// <summary>The group is at its stay: a visit or a caravan at its spot, a Rimfolk visit at its spot out of doors, envoys about the colony, a guest of Hospitality's relaxing.</summary>
        public static bool AtStay(Pawn p, Lord lord, IdleGroup g)
        {
            var toil = lord?.CurLordToil;
            switch (g)
            {
                case IdleGroup.GameVisit:
                case IdleGroup.Caravan:
                    return toil is LordToil_DefendPoint;
                case IdleGroup.RimfolkVisit:
                    return toil is LordToil_VisitHere && !(lord.LordJob is LordJob_RimfolkVisit j && j.indoors);
                case IdleGroup.Envoys:
                    return toil is LordToil_EnvoyStay && p.mindState?.duty?.def == RansomDefOf.Rimfolk_EnvoyStay;
                case IdleGroup.Hospitality:
                    return p.mindState?.duty?.def?.defName == "Relax";
                default:
                    return false;
            }
        }

        private static IdleRole RoleOf(Pawn p, IdleGroup g)
        {
            if (p.trader?.traderKind != null || p.kindDef != null && p.kindDef.trader) return IdleRole.Trader;
            if (g == IdleGroup.Caravan)
                switch (p.GetTraderCaravanRole())
                {
                    case TraderCaravanRole.Trader: return IdleRole.Trader;
                    case TraderCaravanRole.Guard: return IdleRole.Guard;
                }
            return IdleRole.Plain;
        }

        /// <summary>Why this visitor has no idle hour of their own now, or null when they have.</summary>
        public static string WhyNot(Pawn p, out Lord lord, out IdleGroup g)
        {
            lord = null;
            g = IdleGroup.None;
            if (!On) return "the setting or the deeper game hooks are off";
            if (p == null || p.Dead || !p.Spawned || p.Map == null) return "not on a map";
            if (!p.RaceProps.Humanlike || p.DevelopmentalStage.Baby()) return "not a person who can";
            var map = p.Map;
            if (!map.IsPlayerHome) return "not at a home of the colony's";
            if (p.Faction == null || p.Faction.IsPlayer || p.HostileTo(Faction.OfPlayer)) return "not a visitor at peace";
            if (p.IsPrisoner || p.IsSlave || p.IsQuestLodger() || QuestUtility.IsReservedByQuestOrQuestBeingGenerated(p)) return "held, or a quest's";
            if (p.Downed || !p.Awake() || p.Drafted || p.InMentalState || !(p.health?.capacities?.CapableOf(PawnCapacityDefOf.Moving) ?? false)) return "not up and about";
            if (map.dangerWatcher.DangerRating != StoryDanger.None || MapFacts.ThreatOn(map)) return "danger on the map";
            lord = p.GetLord();
            g = GroupOf(p, lord);
            if (g == IdleGroup.None) return "not of a group whose idle hours this fills (" + (lord?.LordJob?.GetType().Name ?? "no group") + ")";
            if (!AtStay(p, lord, g)) return "not at their stay (" + lord.CurLordToil?.GetType().Name + ")";
            if (Outsiders.Leaving(p)) return "leaving";
            if (g == IdleGroup.Caravan && (p.GetTraderCaravanRole() == TraderCaravanRole.Chattel || p.GetTraderCaravanRole() == TraderCaravanRole.Carrier)) return "the caravan's goods";
            if (p.carryTracker?.CarriedThing != null) return "hands full";
            if (Outsiders.TradingNow(p) || OutsiderTalk.Busy(p)) return "trading or talking with the colony";
            if (VisitorTrade.HeldByStory(p)) return "a story holds them";
            if ((p.needs?.food?.CurCategory ?? HungerCategory.Fed) >= HungerCategory.Hungry) return "hungry (their own food first)";
            if ((p.needs?.rest?.CurCategory ?? RestCategory.Rested) >= RestCategory.VeryTired) return "worn out";
            if (g == IdleGroup.RimfolkVisit && VisitorWork.WantsSleep(p)) return "their bedtime";
            if (HealthAIUtility.ShouldSeekMedicalRest(p)) return "ought to rest";
            return null;
        }

        /// <summary>A stint goes on: they are still at their stay, at peace, not leaving, not hungry or worn out; the rain still falls for one sheltering, the fire still burns for one by it, the water still holds fish.</summary>
        public static bool Still(Pawn p, IdleDoing d, Job job)
        {
            if (!On || p?.Map == null || !p.Spawned) return false;
            var lord = p.GetLord();
            var g = GroupOf(p, lord);
            if (g == IdleGroup.None || !AtStay(p, lord, g) || Outsiders.Leaving(p)) return false;
            if (p.Map.dangerWatcher.DangerRating != StoryDanger.None) return false;
            if (d != IdleDoing.Snack && (p.needs?.food?.CurCategory ?? HungerCategory.Fed) >= HungerCategory.UrgentlyHungry) return false;
            if (d != IdleDoing.Nap && d != IdleDoing.Sleep && (p.needs?.rest?.CurCategory ?? RestCategory.Rested) >= RestCategory.VeryTired) return false;
            if (d == IdleDoing.Shelter && !Raining(p.Map)) return false;
            if ((d == IdleDoing.Fire || d == IdleDoing.Warm) && !Lit(job?.targetB.Thing)) return false;
            if (d == IdleDoing.Fish && job != null && !CampLand.Fishable(p.Map, job.targetB.Cell)) return false;
            return true;
        }

        private static bool Raining(Map map) => map.weatherManager.RainRate > 0.1f || map.weatherManager.SnowRate > 0.1f;

        private static bool Lit(Thing fire)
        {
            if (fire == null || !fire.Spawned || fire.IsBurning()) return false;
            var fuel = fire.TryGetComp<CompRefuelable>();
            if (fuel != null && !fuel.HasFuel) return false;
            var flick = fire.TryGetComp<CompFlickable>();
            return flick == null || flick.SwitchIsOn;
        }

        // ---------------- Hospitality's guest area ----------------

        private static bool areaLooked;
        private static Type guestComp;
        private static FieldInfo guestArea;

        /// <summary>
        /// The guest area Hospitality's guest tab set for this guest (Hospitality's own CompGuest.guestArea_int, found by name
        /// as the market finds it), or null when none is set or it isn't Hospitality's guest.
        /// </summary>
        public static Area GuestArea(Pawn p)
        {
            if (!areaLooked)
            {
                areaLooked = true;
                try
                {
                    guestComp = AccessTools.TypeByName("Hospitality.CompGuest");
                    guestArea = guestComp != null ? AccessTools.Field(guestComp, "guestArea_int") : null;
                    if (guestArea != null && guestArea.FieldType != typeof(Area)) guestArea = null;
                }
                catch
                {
                    guestArea = null;
                }
            }
            if (guestArea == null || !(p is ThingWithComps t)) return null;
            try
            {
                foreach (var c in t.AllComps)
                    if (guestComp.IsInstanceOfType(c))
                        return guestArea.GetValue(c) is Area a && a.Map == p.Map && a.TrueCount > 0 ? a : null;
            }
            catch
            {
                // Hospitality's record isn't the one Rimfolk knows: the guest goes wherever they can reach.
            }
            return null;
        }

        // ---------------- What is about them ----------------

        /// <summary>Everything their look found: what is about and near enough, what they carry, their god, the rules' facts.</summary>
        public sealed class Look
        {
            public Pawn p;
            public Map map;
            public Lord lord;
            public IntVec3 focus;
            public float reach;
            /// <summary>Rimfolk's own visits keep out of doors and off the fields.</summary>
            public bool outdoors;
            /// <summary>Hospitality's guest area for them, when one is set: they go nowhere outside it.</summary>
            public Area area;
            /// <summary>The room they stand in now, and where the other visitors are: so they spread out over the colony.</summary>
            public Room room;
            public List<(IntVec3 at, Room room)> others = new List<(IntVec3, Room)>();
            public IdleFacts facts;
            /// <summary>The colony's things on the map, as at the hour's look.</summary>
            public Places places;
            public List<Building> fires = new List<Building>(), seats = new List<Building>(), games = new List<Building>(), music = new List<Building>(),
                stars = new List<Building>(), art = new List<Building>(), shrines = new List<Building>(), tables = new List<Building>(),
                sparTargets = new List<Building>(), archeryTargets = new List<Building>();
            public Thing ownFire;
            public List<Pawn> companions = new List<Pawn>(), colonists = new List<Pawn>(), animals = new List<Pawn>(), pets = new List<Pawn>();
            public Thing food, drug;
            /// <summary>The colony's books near them (one lying about, or a bookcase), and a book of their own.</summary>
            public List<Thing> books = new List<Thing>();
            public Book ownBook;
            /// <summary>Where a walk may end: (where to stand, what to face, the form: a garden, a grave, a view).</summary>
            public List<(IntVec3 stand, IntVec3 face, int form)> walkEnds = new List<(IntVec3, IntVec3, int)>();
            public List<(IntVec3 stand, IntVec3 water)> fishSpots = new List<(IntVec3, IntVec3)>();
            public Apparel worn;
            public ThingWithComps weapon;
            public IntVec3 shade = IntVec3.Invalid, roof = IntVec3.Invalid, roofFace = IntVec3.Invalid;
        }

        /// <summary>What this visitor's idle moment is (the rules' facts and the places about them), or null with why not.</summary>
        public static Look LookFor(Pawn p, out string whyNot)
        {
            whyNot = WhyNot(p, out var lord, out var g);
            if (whyNot != null) return null;
            var map = p.Map;
            var duty = p.mindState?.duty;
            var role = RoleOf(p, g);
            bool hosp = g == IdleGroup.Hospitality;
            var l = new Look
            {
                p = p, map = map, lord = lord,
                // Hospitality's guests reach out over the colony from where they stand, within their guest area.
                focus = !hosp && duty != null && duty.focus.IsValid ? duty.focus.Cell : p.Position,
                reach = IdleHoursMath.Reach(g, role),
                outdoors = g == IdleGroup.RimfolkVisit,
                area = hosp ? GuestArea(p) : null,
                room = p.GetRoom()
            };
            int hour = GenLocalDate.HourOfDay(map);
            bool rain = Raining(map);
            var weather = map.weatherManager.curWeather;
            var f = new IdleFacts
            {
                group = g, role = role, hour = hour, rain = rain,
                age = IdleHoursMath.TheirAge((int)Peoples.TechOf(p.Faction, TechLevel.Undefined)),
                child = p.DevelopmentalStage.Juvenile(),
                titled = p.royalty?.MostSeniorTitle != null || p.Faction?.leader == p,
                calling = Peoples.CallingOf(p.Faction),
                temperature = p.AmbientTemperature,
                underSky = !p.Position.Roofed(map),
                clearSky = !rain && (weather == null || !weather.isBad),
                food = p.needs?.food?.CurLevelPercentage ?? 1f,
                rest = p.needs?.rest?.CurLevelPercentage ?? 1f,
                canTalk = p.health.capacities.CapableOf(PawnCapacityDefOf.Talking),
                canHandle = p.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation),
                canSee = p.health.capacities.GetLevel(PawnCapacityDefOf.Sight) > 0.3f,
                drugsAllowed = DrugsAllowed(p),
                fishingAllowed = FishingAllowed(p)
            };
            try
            {
                f.devout = Worship.Devout(p);
                f.scoffer = Worship.Scoffer(p);
            }
            catch
            {
                f.devout = false;
                f.scoffer = false;
            }
            l.facts = f;
            l.places = PlacesOf(map);
            Others(l);
            Gather(l, l.places);
            f.fire = l.fires.Count > 0 || l.ownFire != null;
            f.seat = l.seats.Count > 0;
            f.game = l.games.Count > 0;
            f.music = l.music.Count > 0;
            f.stars = l.stars.Count > 0;
            f.art = l.art.Count > 0;
            f.shrine = l.shrines.Count > 0;
            f.companion = l.companions.Count > 0;
            f.colonist = l.colonists.Count > 0;
            f.animal = l.animals.Count > 0;
            f.ownFood = l.food != null;
            f.ownDrug = l.drug != null;
            f.book = l.books.Count > 0;
            f.ownBook = l.ownBook != null;
            f.table = l.tables.Count > 0;
            f.trainTarget = l.sparTargets.Count > 0;
            f.openGround = !rain && !IdleHoursMath.Cold(f.temperature);
            f.archeryTarget = l.archeryTargets.Count > 0;
            f.walk = l.walkEnds.Count > 0 || l.places.paths.Any(c => Near(l, c));
            f.pet = l.pets.Count > 0;
            f.water = l.fishSpots.Count > 0;
            f.worn = l.worn != null;
            if (rain && f.underSky)
            {
                l.roof = ShelterCell(l, out l.roofFace);
                f.roof = l.roof.IsValid;
            }
            if (IdleHoursMath.NapHour(hour) && f.rest < IdleHoursMath.SleepsBelow && IdleHoursMath.NapWeather(f.temperature, rain) && role == IdleRole.Plain && !f.titled)
            {
                l.shade = ShadeCell(l);
                f.shade = l.shade.IsValid;
            }
            return l;
        }

        /// <summary>Their faith or their nature doesn't forbid a smoke or a drink: no teetotaller, and with Ideology their faith allows it.</summary>
        private static bool DrugsAllowed(Pawn p)
        {
            try
            {
                if ((p.story?.traits?.DegreeOfTrait(TraitDefOf.DrugDesire) ?? 0) < 0) return false;
                if (ModsConfig.IdeologyActive && p.Ideo != null
                    && (!IdeoUtility.DoerWillingToDo(HistoryEventDefOf.IngestedDrug, p) || !IdeoUtility.DoerWillingToDo(HistoryEventDefOf.IngestedRecreationalDrug, p)))
                    return false;
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Their faith doesn't forbid catching fish (Odyssey's fishing precepts).</summary>
        private static bool FishingAllowed(Pawn p)
        {
            try
            {
                var ev = HistoryEventDefOf.SlaughteredFish;
                return ev == null || !ModsConfig.IdeologyActive || p.Ideo == null || IdeoUtility.DoerWillingToDo(ev, p);
            }
            catch
            {
                return false;
            }
        }

        private static bool Near(Look l, IntVec3 c, float extra = 0f) => c.InHorDistOf(l.focus, l.reach + extra) && (l.area == null || c.InBounds(l.map) && l.area[c]);

        /// <summary>A room no visitor sits about in: a cell, a sickroom, a bedroom or a barracks.</summary>
        private static bool RoomOk(Room room) =>
            room == null || !room.IsPrisonCell && room.Role != RoomRoleDefOf.Bedroom && room.Role != RoomRoleDefOf.Barracks && room.Role != RoomRoleDefOf.Hospital
            && room.Role != RoomRoleDefOf.PrisonBarracks && room.Role != RoomRoleDefOf.PrisonCell;

        /// <summary>A place of the colony's this visitor may be at: near their spot, in a room open to them, kept rooms aside, out of doors for Rimfolk's own visits.</summary>
        private static bool PlaceOk(Look l, Thing t, float extra = 0f)
        {
            if (t == null || !t.Spawned || t.Map != l.map || !Near(l, t.Position, extra)) return false;
            if (l.outdoors && !t.Position.UsesOutdoorTemperature(l.map)) return false;
            if (!RoomOk(t.GetRoom()) || KeepOut.KeptFrom(l.p, t.Position)) return false;
            return !t.IsBurning();
        }

        /// <summary>A cell of the grounds this visitor may go to: near, open to them, kept rooms aside.</summary>
        private static bool GroundOk(Look l, IntVec3 c) =>
            c.IsValid && c.InBounds(l.map) && Near(l, c) && (!l.outdoors || c.UsesOutdoorTemperature(l.map)) && RoomOk(c.GetRoom(l.map)) && !KeepOut.KeptFrom(l.p, c);

        /// <summary>Where the other visitors on the map are (people from outside, not the colony's), for the spread.</summary>
        private static void Others(Look l)
        {
            foreach (var o in l.map.mapPawns.AllPawnsSpawned)
            {
                if (o == null || o == l.p || !o.RaceProps.Humanlike || o.Faction == null || o.Faction.IsPlayer || o.HostileTo(Faction.OfPlayer)) continue;
                // One on their way to a stint counts where they are going.
                var at = o.jobs?.curDriver is JobDriver_VisitIdle && o.CurJob != null && o.CurJob.targetA.IsValid && !o.CurJob.targetA.HasThing ? o.CurJob.targetA.Cell : o.Position;
                l.others.Add((at, at.GetRoom(l.map)));
            }
        }

        /// <summary>How many other visitors are about a cell: within a few steps of it, or in the same room indoors.</summary>
        private static int OthersAt(Look l, IntVec3 c)
        {
            var room = c.GetRoom(l.map);
            bool indoors = room != null && !room.PsychologicallyOutdoors;
            int n = 0;
            foreach (var (at, r) in l.others)
                if (at.InHorDistOf(c, 5f) || indoors && r == room)
                    n++;
            return n;
        }

        /// <summary>The spread's measure of a place for them (IdleHoursMath.PlaceScore): near, another room than this one, few others about, a little chance.</summary>
        private static float Score(Look l, IntVec3 c)
        {
            var room = c.GetRoom(l.map);
            bool same = room != null && room == l.room && !room.PsychologicallyOutdoors;
            return IdleHoursMath.PlaceScore(c.DistanceTo(l.p.Position), Math.Max(8f, l.reach), same, OthersAt(l, c), l.facts.group == IdleGroup.Hospitality, Rand.Value);
        }

        /// <summary>The things in the order the spread picks them, the best first, a few of them.</summary>
        private static List<T> Spread<T>(Look l, IEnumerable<T> items, Func<T, IntVec3> cell, int take) where T : class =>
            items.Select(t => (t, s: Score(l, cell(t)))).OrderBy(x => x.s).Take(take).Select(x => x.t).ToList();

        private static void Gather(Look l, Places pl)
        {
            var p = l.p;
            var map = l.map;
            var f = l.facts;
            foreach (var b in pl.fires)
                if (Lit(b) && PlaceOk(l, b, 4f))
                    l.fires.Add(b);
            if (f.group == IdleGroup.RimfolkVisit && VisitorCamp.FireOf(p) is Thing own && Lit(own)) l.ownFire = own;
            foreach (var b in pl.seats)
                if (PlaceOk(l, b) && !ColonyHas(map, b, b.Position))
                    l.seats.Add(b);
            int theirAge = f.age;
            foreach (var b in pl.games)
                if (PlaceOk(l, b) && FreeJoy(l, b, theirAge))
                    l.games.Add(b);
            foreach (var b in pl.music)
                if (PlaceOk(l, b) && FreeJoy(l, b, theirAge) && !(b is Building_MusicalInstrument m && m.IsBeingPlayed))
                    l.music.Add(b);
            foreach (var b in pl.stars)
                if (PlaceOk(l, b) && !b.Position.Roofed(map) && FreeJoy(l, b, theirAge))
                    l.stars.Add(b);
            foreach (var b in pl.art)
                if (PlaceOk(l, b))
                    l.art.Add(b);
            Shrines(l, pl);
            // One of their own, idle near them.
            if (l.lord != null)
                foreach (var o in l.lord.ownedPawns)
                    if (o != p && o.Spawned && o.RaceProps.Humanlike && !o.DevelopmentalStage.Baby() && Idle(o) && o.Position.InHorDistOf(p.Position, 10f))
                        l.companions.Add(o);
            // One of the colony's people at their leisure, near.
            if (!f.child)
                foreach (var c in map.mapPawns.FreeColonistsSpawned)
                    if (c != null && SmallHabits.CanListen(c) && c.Position.InHorDistOf(p.Position, 14f) && Near(l, c.Position) && CompanyPlace(l, c.Position))
                        l.colonists.Add(c);
            // Their own animals.
            if (l.lord != null)
                foreach (var a in l.lord.ownedPawns)
                    if (a != p && a.Spawned && a.RaceProps.Animal && !a.Downed && !a.Dead && a.Awake() && !a.InMentalState && a.Position.InHorDistOf(p.Position, 12f)
                        && (a.pather == null || !a.pather.Moving))
                        l.animals.Add(a);
            Pack(l);
            Gear(l);
            Books(l, pl);
            foreach (var b in pl.tables)
                if (PlaceOk(l, b) && !ColonyHas(map, b, IntVec3.Invalid))
                    l.tables.Add(b);
            foreach (var b in pl.sparTargets)
                if (PlaceOk(l, b) && FreeTarget(l, b, 1))
                    l.sparTargets.Add(b);
            if (f.weaponKind == 2 && ArrowMote != null)
            {
                f.bow = true;
                foreach (var b in pl.archeryTargets)
                    if (PlaceOk(l, b) && FreeTarget(l, b, 2))
                        l.archeryTargets.Add(b);
            }
            Pets(l);
            WalkEnds(l, pl);
            foreach (var s in pl.fishSpots)
                if (GroundOk(l, s.stand) && CampLand.Fishable(map, s.water))
                    l.fishSpots.Add(s);
        }

        /// <summary>Where one of the colony's people stands is a place to keep them company: open to the visitor, and out of doors for Rimfolk's own visits.</summary>
        private static bool CompanyPlace(Look l, IntVec3 c) =>
            (!l.outdoors || c.UsesOutdoorTemperature(l.map)) && RoomOk(c.GetRoom(l.map)) && !KeepOut.KeptFrom(l.p, c);

        /// <summary>Idle: wandering about, or standing waiting, with nothing else to do.</summary>
        private static bool Idle(Pawn o)
        {
            if (o.Downed || !o.Awake() || o.InMentalState || o.Drafted || OutsiderTalk.Busy(o)) return false;
            var d = o.CurJobDef;
            return d == null || d == JobDefOf.GotoWander || d == JobDefOf.Wait_Wander || d == JobDefOf.StandAndBeSociallyActive;
        }

        /// <summary>A shrine of their god (dedicated by the colony, Gods/Worship/Shrines.cs), or an altar of their faith (Ideology: the colony's own faith is theirs).</summary>
        private static void Shrines(Look l, Places pl)
        {
            var f = l.facts;
            if (f.child || f.scoffer) return;
            try
            {
                var book = Worship.Book;
                var god = Gods.On ? Gods.SwearBy(l.p) : null;
                if (book != null && god != null)
                    foreach (var s in book.shrines)
                        if (s != null && !s.pending && s.Live && s.god == god.id && s.thing is Building b && PlaceOk(l, b))
                            l.shrines.Add(b);
                if (ModsConfig.IdeologyActive && l.p.Ideo != null && (Faction.OfPlayer.ideos?.AllIdeos.Contains(l.p.Ideo) ?? false))
                    foreach (var b in pl.altars)
                        if (!l.shrines.Contains(b) && PlaceOk(l, b))
                            l.shrines.Add(b);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("visitors' idle hours: shrines", e);
            }
        }

        /// <summary>
        /// What they carry of their own: food for a bite (the smallest portion first), a social drug that is a smoke or a
        /// drink, and a book. A trader's pack is the trade's goods, never theirs to eat, drink or read.
        /// </summary>
        private static void Pack(Look l)
        {
            var p = l.p;
            if (l.facts.role == IdleRole.Trader || p.inventory?.innerContainer == null) return;
            float best = float.MaxValue;
            foreach (var t in p.inventory.innerContainer)
            {
                if (t is Book book)
                {
                    if (l.ownBook == null && ReadableBook(book)) l.ownBook = book;
                    continue;
                }
                var def = t?.def;
                if (def?.ingestible == null) continue;
                try
                {
                    if (def.IsDrug)
                    {
                        if (l.drug == null && def.ingestible.drugCategory == DrugCategory.Social && DrugKind(def) != OwnDrugKind.None) l.drug = t;
                        continue;
                    }
                    if (!def.IsNutritionGivingIngestible || !FoodUtility.WillEat(p, t)) continue;
                    float n = t.GetStatValue(StatDefOf.Nutrition);
                    if (n <= 0f || n >= best) continue;
                    best = n;
                    l.food = t;
                }
                catch
                {
                    // Something odd in a pack: leave it there.
                }
            }
        }

        /// <summary>Their own gear: the most worn piece of their clothes, and their weapon by what seeing to it is.</summary>
        private static void Gear(Look l)
        {
            var p = l.p;
            try
            {
                var worn = p.apparel?.WornApparel;
                if (worn != null)
                {
                    float least = 1f;
                    foreach (var a in worn)
                    {
                        if (a?.def == null || !a.def.useHitPoints || !IdleHoursMath.Worn(a.HitPoints, a.MaxHitPoints)) continue;
                        float share = (float)a.HitPoints / Math.Max(1, a.MaxHitPoints);
                        if (share >= least) continue;
                        least = share;
                        l.worn = a;
                    }
                }
                var w = p.equipment?.Primary;
                if (w?.def != null)
                {
                    bool melee = w.def.IsMeleeWeapon;
                    bool sharp = melee && (w.def.tools?.Any(t => t?.capacities != null && t.capacities.Any(c => c != null && (c.defName == "Cut" || c.defName == "Stab"))) ?? false);
                    string projectile = w.def.Verbs?.FirstOrDefault()?.defaultProjectile?.defName;
                    l.facts.weaponKind = IdleHoursMath.WeaponKind(true, melee, sharp, !melee && IdleHoursMath.IsBow(w.def.defName, projectile));
                    if (l.facts.weaponKind != 0) l.weapon = w;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("visitors' idle hours: their gear", e);
            }
        }

        /// <summary>The colony's books near them: one lying where it lies (a shelf, a table, the floor), or a bookcase with books in it; not forbidden, nobody of the colony after it.</summary>
        private static void Books(Look l, Places pl)
        {
            foreach (var t in pl.books)
                if (PlaceOk(l, t) && !t.IsForbidden(Faction.OfPlayer) && !ColonyHas(l.map, t, IntVec3.Invalid) && !Taken(t))
                    l.books.Add(t);
            foreach (var b in pl.bookcases)
                if (PlaceOk(l, b) && !b.IsForbidden(Faction.OfPlayer) && !ColonyHas(l.map, b, IntVec3.Invalid) && b is Building_Bookcase bc && bc.HeldBooks.Any(ReadableBook))
                    l.books.Add(b);
        }

        /// <summary>The colony's pets near them: calm, awake, idle about the place, not roped or worked with, nobody of the colony after them and no other visitor with them.</summary>
        private static void Pets(Look l)
        {
            var map = l.map;
            foreach (var a in map.mapPawns.SpawnedColonyAnimals)
            {
                if (a?.RaceProps == null || a.Dead || a.Downed || !a.Awake() || a.InMentalState || a.Drafted) continue;
                if (!IdleHoursMath.IsPet(a.RaceProps.petness, a.RaceProps.nuzzleMtbHours, a.BodySize)) continue;
                // Not one of the herd the colony ropes and pens (a horse, a yak): a pet about the place.
                if (a.roping != null && a.roping.IsRoped || AnimalPenUtility.NeedsToBeManagedByRope(a)) continue;
                var d = a.CurJobDef;
                if (d != null && d != JobDefOf.GotoWander && d != JobDefOf.Wait_Wander && d != JobDefOf.Wait && d != JobDefOf.Wait_MaintainPosture) continue;
                if (!a.Position.InHorDistOf(l.p.Position, Math.Min(l.reach, 30f)) || !Near(l, a.Position) || !CompanyPlace(l, a.Position)) continue;
                if (ColonyHas(map, a, IntVec3.Invalid) || Taken(a)) continue;
                l.pets.Add(a);
            }
        }

        /// <summary>Where a walk round the colony may end, near enough: a garden, a grave, a view.</summary>
        private static void WalkEnds(Look l, Places pl)
        {
            var map = l.map;
            foreach (var g in pl.gardens)
            {
                if (!Near(l, g)) continue;
                var stand = Beside(l, g);
                if (stand.IsValid) l.walkEnds.Add((stand, g, IdleHoursMath.WalkGarden));
            }
            foreach (var b in pl.graves)
            {
                if (!PlaceOk(l, b)) continue;
                var stand = Beside(l, b.Position);
                if (stand.IsValid) l.walkEnds.Add((stand, b.Position, IdleHoursMath.WalkGrave));
            }
            foreach (var v in pl.views)
                if (GroundOk(l, v.stand))
                    l.walkEnds.Add((v.stand, v.face, IdleHoursMath.WalkView));
        }

        /// <summary>A dry cell beside a place, out of doors, off the fields and free of buildings, to stand at and look at it.</summary>
        private static IntVec3 Beside(Look l, IntVec3 c)
        {
            var map = l.map;
            foreach (var dir in GenAdj.CardinalDirections)
            {
                var s = c + dir;
                if (!s.InBounds(map) || !s.Standable(map) || s.GetEdifice(map) != null || map.zoneManager.ZoneAt(s) is Zone_Growing) continue;
                if (GroundOk(l, s)) return s;
            }
            return IntVec3.Invalid;
        }

        /// <summary>A smoke or a drink, by what the drug is (its smoke, its name, a fluid).</summary>
        public static OwnDrugKind DrugKind(ThingDef def) =>
            def?.ingestible == null ? OwnDrugKind.None
                : IdleHoursMath.DrugKindOf(def.defName, def.ingestible.ingestEffect?.defName, (def.ingestible.foodType & FoodTypeFlags.Fluid) != 0);

        // ---------------- The look of things, by name ----------------

        private static bool looksLooked;
        private static ThingDef arrowMote;
        private static EffecterDef fishingEffect;

        private static void LookUp()
        {
            if (looksLooked) return;
            looksLooked = true;
            // Misc. Training's own arrow (haplo.miscellaneous.training), thrown at the target as its archery throws it.
            arrowMote = DefDatabase<ThingDef>.GetNamedSilentFail("Mote_ArcheryArrow");
            // Odyssey's fishing line and float, shown at the water as the colony's own fishing shows it.
            fishingEffect = DefDatabase<EffecterDef>.GetNamedSilentFail("Fishing");
        }

        /// <summary>The arrow shown flying at the target (Misc. Training's), or null: then no archery, since nothing would show it.</summary>
        public static ThingDef ArrowMote
        {
            get
            {
                LookUp();
                return arrowMote;
            }
        }

        public static EffecterDef FishingEffect
        {
            get
            {
                LookUp();
                return fishingEffect;
            }
        }

        // ---------------- Their next idle hour ----------------

        /// <summary>
        /// One of a visitor group, about to wander near their spot or stand about for want of anything to do, does something
        /// of their own instead when the hour, the weather and what is about allow (IdleHoursMath), most of the time; null
        /// otherwise. <paramref name="anyway"/> for the dev tools: their next look and the chance aside.
        /// <paramref name="standing"/>: the duty would have them stand about (Hospitality's "stand and be sociable"), which only
        /// Hospitality's guests have filled.
        /// </summary>
        public static Job JobFor(Pawn p, bool anyway = false, bool standing = false)
        {
            Fresh();
            if (p == null) return null;
            int now = Now;
            int id = p.thingIDNumber;
            if (!anyway && nextLook.TryGetValue(id, out int next) && now < next && next - now < GenDate.TicksPerDay) return null;
            // Who they are and the chance first; what is about only when it comes to that.
            string why = WhyNot(p, out _, out var group);
            if (why == null && standing && group != IdleGroup.Hospitality) return null;
            if (why != null || !anyway && !Rand.Chance(IdleHoursMath.FillChance(group, GenLocalDate.HourOfDay(p.Map))))
            {
                Later(p, false, 0);
                return null;
            }
            var l = LookFor(p, out _);
            if (l == null)
            {
                Later(p, false, 0);
                return null;
            }
            var w = IdleHoursMath.Weights(l.facts);
            for (int tries = 0; tries < 5; tries++)
            {
                var d = IdleHoursMath.Pick(w, Rand.Value);
                if (d == IdleDoing.None) break;
                Job job = null;
                try
                {
                    job = Make(l, d);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("visitors' idle hours: " + d, e);
                }
                if (job != null)
                {
                    var (lo, hi) = IdleHoursMath.Stint(d);
                    Later(p, true, hi);
                    return job;
                }
                w[(int)d] = 0f;
            }
            Later(p, false, 0);
            return null;
        }

        private static void Later(Pawn p, bool found, int stint)
        {
            if (nextLook.Count > 400) nextLook.Clear();
            var (lo, hi) = IdleHoursMath.NextLook(found);
            nextLook[p.thingIDNumber] = Now + stint + Rand.RangeInclusive(lo, hi);
        }

        /// <summary>The job for one idle doing, with its place found (the way there checked), or null when there's none for them now.</summary>
        private static Job Make(Look l, IdleDoing d)
        {
            var p = l.p;
            switch (d)
            {
                case IdleDoing.Chat:
                    foreach (var o in l.companions.OrderBy(o => o.Position.DistanceToSquared(p.Position)))
                        if (p.CanReach(o, PathEndMode.Touch, Danger.Some))
                            return JobMaker.MakeJob(IdleDefOf.Rimfolk_IdleChat, o);
                    return null;
                case IdleDoing.Company:
                    foreach (var c in l.colonists.OrderBy(c => c.Position.DistanceToSquared(p.Position)))
                        if (p.CanReach(c, PathEndMode.Touch, Danger.Some))
                            return JobMaker.MakeJob(IdleDefOf.Rimfolk_IdleCompany, c);
                    return null;
                case IdleDoing.Fire:
                case IdleDoing.Warm:
                {
                    var fires = new List<Thing>();
                    if (l.ownFire != null) fires.Add(l.ownFire);
                    // To warm the hands the nearest fire; to sit by one, the spread's pick of them.
                    fires.AddRange(d == IdleDoing.Warm ? l.fires.OrderBy(b => b.Position.DistanceToSquared(p.Position)).Take(4) : Spread(l, l.fires, b => b.Position, 4));
                    foreach (var fire in fires)
                    {
                        var cell = RingCell(l, fire, d == IdleDoing.Warm);
                        if (cell.IsValid) return JobMaker.MakeJob(d == IdleDoing.Warm ? IdleDefOf.Rimfolk_IdleWarm : IdleDefOf.Rimfolk_IdleFire, cell, fire);
                    }
                    return null;
                }
                case IdleDoing.Seat:
                {
                    // Out in the open in fair weather, under a roof in foul; over the colony, not all in the one room.
                    bool fair = !l.facts.rain && !IdleHoursMath.Cold(l.facts.temperature);
                    foreach (var s in l.seats.Select(s => (s, k: (s.Position.UsesOutdoorTemperature(l.map) == fair ? 0f : 1f) + Score(l, s.Position))).OrderBy(x => x.k).Take(6).Select(x => x.s))
                        if (StandOk(l, s.Position, true))
                            return JobMaker.MakeJob(IdleDefOf.Rimfolk_IdleSeat, s.Position, s.Position + s.Rotation.FacingCell);
                    return null;
                }
                case IdleDoing.Shelter:
                    return l.roof.IsValid ? JobMaker.MakeJob(IdleDefOf.Rimfolk_IdleShelter, l.roof, l.roofFace) : null;
                case IdleDoing.Snack:
                case IdleDoing.OwnDrug:
                case IdleDoing.Pipe:
                case IdleDoing.Carve:
                case IdleDoing.Mend:
                case IdleDoing.Sharpen:
                case IdleDoing.Sing:
                {
                    var what = d == IdleDoing.Snack ? l.food : d == IdleDoing.OwnDrug ? l.drug : null;
                    if ((d == IdleDoing.Snack || d == IdleDoing.OwnDrug) && what == null) return null;
                    if (d == IdleDoing.Mend && l.worn == null || d == IdleDoing.Sharpen && l.weapon == null) return null;
                    var cell = RestCell(l, out var face);
                    if (!cell.IsValid) return null;
                    var def = d == IdleDoing.Snack ? IdleDefOf.Rimfolk_IdleSnack : d == IdleDoing.OwnDrug ? IdleDefOf.Rimfolk_IdleOwnDrug
                        : d == IdleDoing.Pipe ? IdleDefOf.Rimfolk_IdlePipe : d == IdleDoing.Carve ? IdleDefOf.Rimfolk_IdleCarve
                        : d == IdleDoing.Mend ? IdleDefOf.Rimfolk_IdleMend : d == IdleDoing.Sharpen ? IdleDefOf.Rimfolk_IdleSharpen : IdleDefOf.Rimfolk_IdleSing;
                    var job = JobMaker.MakeJob(def, cell, face);
                    job.thingDefToCarry = what?.def;
                    if (d == IdleDoing.Mend) job.targetC = l.worn;
                    if (d == IdleDoing.Sharpen)
                    {
                        job.targetC = l.weapon;
                        job.count = IdleHoursMath.SharpenForm(l.facts.weaponKind);
                    }
                    return job;
                }
                case IdleDoing.Nap:
                {
                    if (!l.shade.IsValid) return null;
                    var job = JobMaker.MakeJob(IdleDefOf.Rimfolk_IdleNap, l.shade);
                    var (lo, hi) = IdleHoursMath.Stint(d);
                    job.expiryInterval = Rand.RangeInclusive(lo, hi);
                    job.checkOverrideOnExpire = false;
                    return job;
                }
                case IdleDoing.Sleep:
                {
                    var cell = SleepCell(l);
                    if (!cell.IsValid) return null;
                    var job = JobMaker.MakeJob(IdleDefOf.Rimfolk_IdleSleep, cell);
                    var (lo, hi) = IdleHoursMath.Stint(d);
                    job.expiryInterval = Rand.RangeInclusive(lo, hi);
                    job.checkOverrideOnExpire = false;
                    return job;
                }
                case IdleDoing.Game:
                case IdleDoing.Music:
                case IdleDoing.Stars:
                {
                    var list = d == IdleDoing.Game ? l.games : d == IdleDoing.Music ? l.music : l.stars;
                    var def = d == IdleDoing.Game ? IdleDefOf.Rimfolk_IdleGame : d == IdleDoing.Music ? IdleDefOf.Rimfolk_IdleMusic : IdleDefOf.Rimfolk_IdleStars;
                    foreach (var b in Spread(l, list, b => b.Position, 5))
                    {
                        var j = JoyOf(b.def);
                        if (j == null) continue;
                        var cell = JoyCell(l, b, j);
                        if (cell.IsValid) return JobMaker.MakeJob(def, cell, b);
                    }
                    return null;
                }
                case IdleDoing.Art:
                    foreach (var b in Spread(l, l.art, b => b.Position, 5))
                    {
                        var cell = ViewCell(l, b);
                        if (cell.IsValid) return JobMaker.MakeJob(IdleDefOf.Rimfolk_IdleArt, cell, b);
                    }
                    return null;
                case IdleDoing.Pray:
                    foreach (var b in l.shrines.OrderBy(b => b.Position.DistanceToSquared(p.Position)).Take(3))
                    {
                        var cell = b.def.hasInteractionCell && StandOk(l, b.InteractionCell, false, true) ? b.InteractionCell : NextTo(l, b);
                        if (cell.IsValid) return JobMaker.MakeJob(IdleDefOf.Rimfolk_IdlePray, cell, b);
                    }
                    return null;
                case IdleDoing.Animal:
                    foreach (var a in l.animals.OrderBy(a => a.Position.DistanceToSquared(p.Position)))
                        if (p.CanReach(a, PathEndMode.Touch, Danger.Some))
                            return JobMaker.MakeJob(IdleDefOf.Rimfolk_IdleAnimal, a);
                    return null;
                case IdleDoing.Play:
                {
                    var route = PlayRoute(l);
                    if (route.Count < 2) return null;
                    var job = JobMaker.MakeJob(IdleDefOf.Rimfolk_IdlePlay);
                    job.targetQueueA = route.Select(c => new LocalTargetInfo(c)).ToList();
                    job.locomotionUrgency = LocomotionUrgency.Jog;
                    return job;
                }
                case IdleDoing.Read:
                    return ReadJob(l);
                case IdleDoing.Walk:
                    return WalkJob(l);
                case IdleDoing.Pet:
                    foreach (var a in l.pets.OrderBy(a => a.Position.DistanceToSquared(p.Position)).Take(4))
                        if (p.CanReach(a, PathEndMode.Touch, Danger.Some))
                        {
                            var job = JobMaker.MakeJob(IdleDefOf.Rimfolk_IdlePet, a);
                            // A dog is thrown a stick now and then; the rest are petted.
                            job.count = IdleHoursMath.PlaysFetch(a.def.defName) && a.Position.UsesOutdoorTemperature(l.map) && Rand.Chance(0.5f) ? 1 : 0;
                            return job;
                        }
                    return null;
                case IdleDoing.Write:
                    foreach (var t in Spread(l, l.tables, b => b.Position, 5))
                    {
                        var seat = TableSeat(l, t);
                        if (!seat.IsValid) continue;
                        var job = JobMaker.MakeJob(IdleDefOf.Rimfolk_IdleWrite, seat, t);
                        job.count = Rand.Chance(0.4f) ? 1 : 0;
                        return job;
                    }
                    return null;
                case IdleDoing.Train:
                {
                    foreach (var t in Spread(l, l.sparTargets, b => b.Position, 3))
                    {
                        var cell = TargetCell(l, t, false);
                        if (cell.IsValid) return JobMaker.MakeJob(IdleDefOf.Rimfolk_IdleTrain, cell, t);
                    }
                    if (!l.facts.openGround) return null;
                    var open = OpenGround(l);
                    if (!open.IsValid) return null;
                    var shadow = JobMaker.MakeJob(IdleDefOf.Rimfolk_IdleTrain, open, open + p.Rotation.FacingCell);
                    shadow.count = 1;
                    return shadow;
                }
                case IdleDoing.Archery:
                    foreach (var t in Spread(l, l.archeryTargets, b => b.Position, 3))
                    {
                        var cell = TargetCell(l, t, true);
                        if (cell.IsValid) return JobMaker.MakeJob(IdleDefOf.Rimfolk_IdleArchery, cell, t);
                    }
                    return null;
                case IdleDoing.Fish:
                    foreach (var s in l.fishSpots.Select(x => (x, k: Score(l, x.stand))).OrderBy(x => x.k).Take(5).Select(x => x.x))
                        if (StandOk(l, s.stand, false, true, 4f))
                            return JobMaker.MakeJob(IdleDefOf.Rimfolk_IdleFish, s.stand, s.water);
                    return null;
                case IdleDoing.Cards:
                    foreach (var o in l.companions.Where(o => !o.DevelopmentalStage.Juvenile()).OrderBy(o => o.Position.DistanceToSquared(p.Position)))
                        if (p.CanReach(o, PathEndMode.Touch, Danger.Some))
                        {
                            var job = JobMaker.MakeJob(IdleDefOf.Rimfolk_IdleCards, o);
                            job.count = IdleHoursMath.CardsForm(l.facts.age, Rand.Value);
                            return job;
                        }
                    return null;
                default:
                    return null;
            }
        }

        /// <summary>
        /// A book to read: one of their own from their pack (read where they sit down), or one of the colony's where it lies
        /// or in its bookcase, read on a seat close by and put back where it was after (target A the book, B where they read,
        /// C its bookcase or the cell it lay on; the job's count says which).
        /// </summary>
        private static Job ReadJob(Look l)
        {
            var p = l.p;
            bool own = l.ownBook != null && (l.books.Count == 0 || Rand.Chance(0.6f));
            if (own)
            {
                var cell = RestCell(l, out _);
                if (!cell.IsValid) return null;
                var job = JobMaker.MakeJob(IdleDefOf.Rimfolk_IdleRead, l.ownBook, cell);
                job.count = IdleHoursMath.ReadOwn;
                return job;
            }
            foreach (var place in Spread(l, l.books, t => t.Position, 4))
            {
                Book book = place as Book;
                if (place is Building_Bookcase bc) book = bc.HeldBooks.Where(b => ReadableBook(b) && !Taken(b)).RandomElementWithFallback();
                if (book == null) continue;
                if (!p.CanReach(place, PathEndMode.Touch, Danger.Some)) continue;
                var read = SeatNear(l, place.Position, 8f);
                if (!read.IsValid) read = NextTo(l, place);
                if (!read.IsValid) continue;
                var job = JobMaker.MakeJob(IdleDefOf.Rimfolk_IdleRead, book, read);
                job.targetC = place is Building_Bookcase ? new LocalTargetInfo(place) : new LocalTargetInfo(place.Position);
                job.count = IdleHoursMath.ReadColony;
                return job;
            }
            return null;
        }

        /// <summary>
        /// A walk round the colony: two or three places on its paths (or its open ground) on the way, and the walk's end, a
        /// garden, a grave or a view, where they stand a while looking at it; with no such end near, round the paths only.
        /// </summary>
        private static Job WalkJob(Look l)
        {
            var p = l.p;
            var map = l.map;
            (IntVec3 stand, IntVec3 face, int form) end = (IntVec3.Invalid, IntVec3.Invalid, IdleHoursMath.WalkPaths);
            foreach (var e in l.walkEnds.Select(x => (x, k: Score(l, x.stand) + (x.form == IdleHoursMath.WalkView ? 0.2f : 0f))).OrderBy(x => x.k).Take(5).Select(x => x.x))
                if (StandOk(l, e.stand, false, true, 4f))
                {
                    end = e;
                    break;
                }
            var route = new List<IntVec3>();
            var paths = l.places.paths.Where(c => Near(l, c) && c.DistanceTo(p.Position) > 5f).InRandomOrder().Take(12).ToList();
            foreach (var c in paths)
            {
                if (route.Count >= (end.stand.IsValid ? 2 : 4)) break;
                if (route.Any(r => r.DistanceToSquared(c) < 36) || end.stand.IsValid && c.DistanceToSquared(end.stand) < 25) continue;
                if (StandOk(l, c, false, true, 4f)) route.Add(c);
            }
            if (route.Count == 0 && !end.stand.IsValid) return null;
            if (route.Count < 2 && !end.stand.IsValid)
            {
                // Round open ground when there are few paths laid.
                foreach (var c in GenRadial.RadialCellsAround(p.Position, Math.Min(18f, l.reach), false).InRandomOrder().Take(40))
                {
                    if (route.Count >= 3) break;
                    if (!c.InBounds(map) || !c.UsesOutdoorTemperature(map) || c.GetEdifice(map) != null || route.Any(r => r.DistanceToSquared(c) < 36)) continue;
                    if (StandOk(l, c, false, false, 4f)) route.Add(c);
                }
                if (route.Count < 2) return null;
            }
            // Nearest first, so the walk goes round rather than back and forth.
            var ordered = new List<IntVec3>();
            var from = p.Position;
            var left = new List<IntVec3>(route);
            while (left.Count > 0)
            {
                var next = left.OrderBy(c => c.DistanceToSquared(from)).First();
                ordered.Add(next);
                left.Remove(next);
                from = next;
            }
            if (end.stand.IsValid) ordered.Add(end.stand);
            var job = JobMaker.MakeJob(IdleDefOf.Rimfolk_IdleWalk);
            job.targetQueueA = ordered.Select(c => new LocalTargetInfo(c)).ToList();
            job.targetB = end.face;
            job.count = end.stand.IsValid ? end.form : IdleHoursMath.WalkPaths;
            job.locomotionUrgency = LocomotionUrgency.Walk;
            return job;
        }

        // ---------------- Places to stand ----------------

        /// <summary>
        /// A cell this visitor may stand (or sit) on for a while: open to them (no cell, sickroom, bedroom or kept room; out
        /// of doors and off the fields for Rimfolk's own visits; inside their guest area for Hospitality's guests), near their
        /// spot, nobody on it, nobody of theirs going to it, nothing of the colony's reserved there or on its way to it, not
        /// where someone works at one of the colony's buildings (a cook's spot by the campfire, a crafter's at the bench), and
        /// within reach. <paramref name="seat"/>: a seat's cell, where the seat itself is the edifice; <paramref name="spot"/>:
        /// the place's own spot (a game's, an instrument's, a shrine's), which is a building's spot by its nature.
        /// </summary>
        private static bool StandOk(Look l, IntVec3 c, bool seat, bool spot = false, float extra = 4f)
        {
            var map = l.map;
            var p = l.p;
            if (!c.IsValid || !c.InBounds(map) || !c.Standable(map) || c.Fogged(map)) return false;
            if (!Near(l, c, extra)) return false;
            if (!spot && l.places != null && l.places.workCells.Contains(map.cellIndices.CellToIndex(c))) return false;
            var edifice = c.GetEdifice(map);
            if (edifice is Building_Door) return false;
            if (edifice != null && !seat && !(edifice.def.building?.isSittable ?? false) && edifice.def.passability != Traversability.Standable) return false;
            if (l.outdoors && (!c.UsesOutdoorTemperature(map) || map.zoneManager.ZoneAt(c) is Zone_Growing)) return false;
            if (!RoomOk(c.GetRoom(map)) || KeepOut.KeptFrom(p, c)) return false;
            if (c.GetFirstPawn(map) is Pawn there && there != p) return false;
            if (Taken(c, p) || ColonyHas(map, edifice, c)) return false;
            if (c.ContainsStaticFire(map)) return false;
            return p.CanReach(c, PathEndMode.OnCell, Danger.Some);
        }

        /// <summary>Another visitor at a stint has this cell.</summary>
        private static bool Taken(IntVec3 c, Pawn p)
        {
            for (int i = busy.Count - 1; i >= 0; i--)
            {
                var o = busy[i];
                if (o == null || o.Dead || !o.Spawned || !(o.jobs?.curDriver is JobDriver_VisitIdle))
                {
                    busy.RemoveAt(i);
                    continue;
                }
                if (o != p && o.Map == p.Map && o.CurJob != null && o.CurJob.targetA.Cell == c) return true;
            }
            return false;
        }

        /// <summary>Another visitor at a stint has this thing (a book, a pet).</summary>
        private static bool Taken(Thing t)
        {
            foreach (var o in busy)
                if (o != null && o.Spawned && o.CurJob != null && o.jobs.curDriver is JobDriver_VisitIdle && (o.CurJob.targetA.Thing == t || o.CurJob.targetC.Thing == t))
                    return true;
            return false;
        }

        /// <summary>How many visitors at a stint are at this thing.</summary>
        private static int AtIt(Thing t)
        {
            int n = 0;
            foreach (var o in busy)
                if (o != null && o.Spawned && o.CurJob != null && o.CurJob.targetB.Thing == t && o.jobs.curDriver is JobDriver_VisitIdle)
                    n++;
            return n;
        }

        /// <summary>
        /// One of the colony's people has this thing or this place, or is on their way to it: reserved by them (the thing,
        /// the cell, the seat on it), or the cell they're walking to.
        /// </summary>
        public static bool ColonyHas(Map map, Thing t, IntVec3 c) => ColonyWho(map, t, c, out _);

        public static bool ColonyWho(Map map, Thing t, IntVec3 c, out Pawn who)
        {
            who = null;
            if (map == null) return false;
            var player = Faction.OfPlayer;
            var rm = map.reservationManager;
            if (t != null && t.Spawned && rm.TryGetReserver(t, player, out who)) return true;
            if (c.IsValid && c.InBounds(map))
            {
                if (rm.TryGetReserver(c, player, out who)) return true;
                var seat = c.GetEdifice(map);
                if (seat != null && seat != t && rm.TryGetReserver(seat, player, out who)) return true;
                who = map.pawnDestinationReservationManager.FirstReserverOf(c, player);
                if (who != null) return true;
            }
            return false;
        }

        /// <summary>A joy thing is free for this visitor: nobody of the colony has it, the player hasn't forbidden it or switched it off, it has power, there's room at it, it's of their people's age, and they didn't just give it up.</summary>
        private static bool FreeJoy(Look l, Building b, int theirAge)
        {
            var j = JoyOf(b.def);
            if (j == null || !IdleHoursMath.Fits(theirAge, j.age)) return false;
            if (b.IsBrokenDown()) return false;
            if (gaveWay.TryGetValue(l.p.thingIDNumber, out var gw) && gw.thing == b.thingIDNumber && Now < gw.until) return false;
            var power = b.TryGetComp<CompPowerTrader>();
            var flick = b.TryGetComp<CompFlickable>();
            return IdleHoursMath.Free(ColonyHas(l.map, b, IntVec3.Invalid), b.IsForbidden(Faction.OfPlayer), flick != null && !flick.SwitchIsOn, power != null && !power.PowerOn,
                AtIt(b), j.participants);
        }

        /// <summary>A training target is free: nobody of the colony at it or on the way, not forbidden, room for one more of them, not just given up.</summary>
        private static bool FreeTarget(Look l, Building b, int room)
        {
            if (gaveWay.TryGetValue(l.p.thingIDNumber, out var gw) && gw.thing == b.thingIDNumber && Now < gw.until) return false;
            return IdleHoursMath.Free(ColonyHas(l.map, b, IntVec3.Invalid), b.IsForbidden(Faction.OfPlayer), false, false, AtIt(b), room);
        }

        /// <summary>Where to stand or sit at a joy thing, as the game's joy giver for it would: a watching spot, a seat beside it, its own spot.</summary>
        private static IntVec3 JoyCell(Look l, Building b, JoyThing j)
        {
            switch (j.shape)
            {
                case JoyShape.Watch:
                    try
                    {
                        if (WatchBuildingUtility.TryFindBestWatchCell(b, l.p, j.desireSit, out var c, out var chair) && StandOk(l, c, chair != null)) return c;
                    }
                    catch
                    {
                        // A mod's odd building: try beside it instead.
                    }
                    return NextTo(l, b);
                case JoyShape.Cell:
                case JoyShape.Instrument:
                    if (b.def.hasInteractionCell)
                        return StandOk(l, b.InteractionCell, true, true) ? b.InteractionCell : IntVec3.Invalid;
                    return NextTo(l, b);
                default:
                {
                    // Beside it: a seat there first, as the game sits its players.
                    IntVec3 best = IntVec3.Invalid;
                    foreach (var c in GenAdj.CellsAdjacentCardinal(b).InRandomOrder())
                    {
                        var e = c.InBounds(l.map) ? c.GetEdifice(l.map) : null;
                        bool sits = e?.def.building != null && e.def.building.isSittable;
                        if (!StandOk(l, c, sits)) continue;
                        if (sits) return c;
                        if (!best.IsValid) best = c;
                    }
                    return best;
                }
            }
        }

        /// <summary>
        /// Where to stand at a training target: for archery the game's own watching spot for it (Misc. Training's archery stand
        /// is shot at from six to nine cells off), for a target or a dummy the cell in front of it (or beside it).
        /// </summary>
        private static IntVec3 TargetCell(Look l, Building b, bool archery)
        {
            try
            {
                if (b.def.building != null && (archery || b.def.building.watchBuildingStandDistanceRange.max > 0)
                    && WatchBuildingUtility.TryFindBestWatchCell(b, l.p, false, out var c, out _) && StandOk(l, c, false, false, archery ? 10f : 4f)) return c;
            }
            catch
            {
                // A mod's odd target: beside it instead.
            }
            return archery ? IntVec3.Invalid : NextTo(l, b);
        }

        /// <summary>A free cell beside a thing (cardinal first).</summary>
        private static IntVec3 NextTo(Look l, Thing t)
        {
            foreach (var c in GenAdj.CellsAdjacentCardinal(t).InRandomOrder())
                if (StandOk(l, c, false))
                    return c;
            foreach (var c in GenAdj.CellsAdjacent8Way(t).InRandomOrder())
                if (StandOk(l, c, false))
                    return c;
            return IntVec3.Invalid;
        }

        /// <summary>A place in the ring round a fire (one to three steps off), or right beside it to warm the hands; a seat there first.</summary>
        private static IntVec3 RingCell(Look l, Thing fire, bool close)
        {
            var map = l.map;
            IntVec3 best = IntVec3.Invalid;
            float inner = close ? 0.9f : 1.4f, outer = close ? 1.6f : 3.1f;
            foreach (var c in GenRadial.RadialCellsAround(fire.Position, outer, false).InRandomOrder())
            {
                float dist = c.DistanceTo(fire.Position);
                if (dist < inner || !c.InBounds(map)) continue;
                var e = c.GetEdifice(map);
                bool sits = e?.def.building != null && e.def.building.isSittable;
                if (!StandOk(l, c, sits)) continue;
                if (sits) return c;
                if (!best.IsValid) best = c;
            }
            return best;
        }

        /// <summary>A cell a step or two from a piece of art, in sight of it.</summary>
        private static IntVec3 ViewCell(Look l, Thing art)
        {
            var map = l.map;
            foreach (var c in GenRadial.RadialCellsAround(art.Position, 2.3f, false).InRandomOrder())
            {
                if (!c.InBounds(map) || art.OccupiedRect().Contains(c) || !StandOk(l, c, false)) continue;
                if (GenSight.LineOfSight(c, art.Position, map, true)) return c;
            }
            return IntVec3.Invalid;
        }

        /// <summary>Under a roof near them, out of the rain or snow: a porch or a shed's eaves out of doors for Rimfolk's own visits, a common room for the rest; and the open cell to look out at.</summary>
        private static IntVec3 ShelterCell(Look l, out IntVec3 face)
        {
            face = IntVec3.Invalid;
            var map = l.map;
            var from = l.p.Position;
            int looked = 0;
            foreach (var c in GenRadial.RadialCellsAround(from, Math.Min(12f, l.reach + 4f), true))
            {
                if (++looked > 160) break;
                if (!c.InBounds(map) || !c.Roofed(map)) continue;
                if (l.outdoors && !c.UsesOutdoorTemperature(map)) continue;
                if (!StandOk(l, c, false)) continue;
                face = GenAdj.CardinalDirections.Select(dir => c + dir).FirstOrDefault(n => n.InBounds(map) && !n.Roofed(map));
                if (!face.IsValid) face = c + l.p.Rotation.FacingCell;
                return c;
            }
            return IntVec3.Invalid;
        }

        /// <summary>The shade of a tree near their spot, out of doors and off the fields: a cell beside its trunk.</summary>
        private static IntVec3 ShadeCell(Look l)
        {
            var map = l.map;
            int trees = 0;
            foreach (var c in GenRadial.RadialCellsAround(l.focus, Math.Min(16f, l.reach), true).InRandomOrder())
            {
                if (!c.InBounds(map) || !(c.GetPlant(map) is Plant tree) || tree.def.plant == null || !tree.def.plant.IsTree || tree.Growth < 0.5f) continue;
                if (map.zoneManager.ZoneAt(c) is Zone_Growing || ++trees > 12) continue;
                foreach (var n in GenAdj.CellsAdjacentCardinal(c, Rot4.North, IntVec2.One).InRandomOrder())
                    if (n.InBounds(map) && n.UsesOutdoorTemperature(map) && !n.Roofed(map) && !(map.zoneManager.ZoneAt(n) is Zone_Growing) && n.GetEdifice(map) == null
                        && StandOk(l, n, false))
                        return n;
            }
            return IntVec3.Invalid;
        }

        /// <summary>
        /// Where to lie down for a night's sleep near their spot: bare ground out of doors with nothing built on it, off the
        /// fields; under a roof (a porch, a shed's eaves) in rain, snow or the cold when there is one. Never inside the
        /// colony's rooms: a visitor asleep on the dining room floor would be in everyone's way.
        /// </summary>
        private static IntVec3 SleepCell(Look l)
        {
            var map = l.map;
            bool roofed = l.facts.rain || IdleHoursMath.Cold(l.facts.temperature);
            for (int pass = roofed ? 0 : 1; pass < 2; pass++)
            {
                int looked = 0;
                foreach (var c in GenRadial.RadialCellsAround(l.focus, Math.Min(10f, l.reach), true))
                {
                    if (++looked > 200) break;
                    if (!c.InBounds(map) || !c.UsesOutdoorTemperature(map) || c.GetEdifice(map) != null || map.zoneManager.ZoneAt(c) is Zone_Growing) continue;
                    // First a roof over them in foul weather; else (or with no roof near) the open ground.
                    if (pass == 0 && !c.Roofed(map) || pass == 1 && roofed && c.Roofed(map)) continue;
                    if (StandOk(l, c, false)) return c;
                }
            }
            return IntVec3.Invalid;
        }

        /// <summary>Open ground out of doors near them, bare and free, to spar on.</summary>
        private static IntVec3 OpenGround(Look l)
        {
            var map = l.map;
            foreach (var c in GenRadial.RadialCellsAround(l.p.Position, Math.Min(12f, l.reach), true).InRandomOrder().Take(60))
            {
                if (!c.InBounds(map) || !c.UsesOutdoorTemperature(map) || c.Roofed(map) || c.GetEdifice(map) != null || map.zoneManager.ZoneAt(c) is Zone_Growing) continue;
                if (OthersAt(l, c) > 0) continue;
                if (StandOk(l, c, false)) return c;
            }
            return IntVec3.Invalid;
        }

        /// <summary>A free seat near a place (within a few steps), the spread's pick.</summary>
        private static IntVec3 SeatNear(Look l, IntVec3 at, float within)
        {
            foreach (var s in l.seats.Where(s => s.Position.InHorDistOf(at, within)).OrderBy(s => s.Position.DistanceToSquared(at)).Take(4))
                if (StandOk(l, s.Position, true))
                    return s.Position;
            return IntVec3.Invalid;
        }

        /// <summary>A free seat at a table (beside it, a chair or a stool), to write at.</summary>
        private static IntVec3 TableSeat(Look l, Building table)
        {
            foreach (var c in GenAdj.CellsAdjacentCardinal(table).InRandomOrder())
            {
                if (!c.InBounds(l.map)) continue;
                var e = c.GetEdifice(l.map);
                if (e?.def.building == null || !e.def.building.isSittable || e is Building_Bed) continue;
                if (StandOk(l, c, true)) return c;
            }
            return IntVec3.Invalid;
        }

        /// <summary>
        /// Where to sit for a bite, a smoke, a bit of whittling, mending or a song: a free seat somewhere about the colony
        /// (the spread's pick, not far), else beside their own camp's fire, else where they stand, else a free cell next to
        /// it; and what to face.
        /// </summary>
        private static IntVec3 RestCell(Look l, out IntVec3 face)
        {
            var p = l.p;
            face = IntVec3.Invalid;
            float within = l.facts.group == IdleGroup.Hospitality ? 20f : 8f;
            foreach (var s in Spread(l, l.seats.Where(s => s.Position.InHorDistOf(p.Position, within)), s => s.Position, 4))
                if (StandOk(l, s.Position, true))
                {
                    face = s.Position + s.Rotation.FacingCell;
                    return s.Position;
                }
            if (l.ownFire != null && l.ownFire.Position.InHorDistOf(p.Position, 8f))
            {
                var c = RingCell(l, l.ownFire, false);
                if (c.IsValid)
                {
                    face = l.ownFire.Position;
                    return c;
                }
            }
            if (StandOk(l, p.Position, false))
            {
                face = p.Position + p.Rotation.FacingCell;
                return p.Position;
            }
            foreach (var c in GenAdj.CellsAdjacent8Way(p).InRandomOrder())
                if (StandOk(l, c, false))
                {
                    face = c + (c - p.Position);
                    return c;
                }
            return IntVec3.Invalid;
        }

        /// <summary>A child's round of play: a few places a few steps apart near where they are, to run between.</summary>
        private static List<IntVec3> PlayRoute(Look l)
        {
            var map = l.map;
            var route = new List<IntVec3>();
            int want = Rand.RangeInclusive(3, 5);
            foreach (var c in GenRadial.RadialCellsAround(l.p.Position, 6f, false).InRandomOrder())
            {
                if (route.Count >= want) break;
                if (!c.InBounds(map) || c.GetEdifice(map) != null || route.Any(r => r.DistanceToSquared(c) < 9)) continue;
                if (!StandOk(l, c, false)) continue;
                route.Add(c);
            }
            return route;
        }

        // ---------------- At a stint ----------------

        /// <summary>A stint has begun: counted, so two of them don't go for one place, and for the log.</summary>
        public static void Started(Pawn p)
        {
            Fresh();
            if (p == null) return;
            if (!busy.Contains(p)) busy.Add(p);
            var t = TallyOf(p);
            t.startedAt = Now;
            t.stints++;
        }

        /// <summary>A stint is over: the next look comes at once (a while later when it ended almost as it began).</summary>
        public static void Ended(Pawn p)
        {
            if (p == null) return;
            busy.Remove(p);
            int now = Now;
            int ran = IdleHoursMath.ShortStint;
            if (tallies.TryGetValue(p.thingIDNumber, out var t) && t.startedAt >= 0 && now >= t.startedAt)
            {
                ran = now - t.startedAt;
                t.stint += Math.Min(ran, GenDate.TicksPerHour * 8);
                t.startedAt = -1;
            }
            var (lo, hi) = IdleHoursMath.NextLookAfter(ran);
            if (nextLook.Count > 400) nextLook.Clear();
            nextLook[p.thingIDNumber] = now + Rand.RangeInclusive(lo, hi);
        }

        /// <summary>
        /// For the log: the duty asked this visitor for a job. The standing about since the last ask is counted (a stretch
        /// at most), and a new stretch starts when they are left to stand or wander now.
        /// </summary>
        public static void Asked(Pawn p, bool leftIdle)
        {
            if (p == null) return;
            Fresh();
            if (!tallies.TryGetValue(p.thingIDNumber, out var t))
            {
                // Only visitors of a group whose idle hours this fills are counted (not raiders, camps or the market's own).
                if (!leftIdle || p.Map == null || GroupOf(p, p.GetLord()) == IdleGroup.None) return;
                t = TallyOf(p);
            }
            int now = Now;
            if (t.idleSince >= 0 && now >= t.idleSince) t.idle += Math.Min(now - t.idleSince, IdleHoursMath.IdleMomentCap);
            t.idleSince = leftIdle ? now : -1;
        }

        /// <summary>They gave up a joy thing to one of the colony's people: not tried again for a few hours.</summary>
        public static void GaveWay(Pawn p, Thing t)
        {
            if (p == null || t == null) return;
            if (gaveWay.Count > 200) gaveWay.Clear();
            gaveWay[p.thingIDNumber] = (t.thingIDNumber, Now + IdleHoursMath.GiveWayHours * GenDate.TicksPerHour);
        }

        // ---------------- What's said ----------------

        /// <summary>
        /// A line from them as a stint begins (one sentence, Rimfolk_Pool_VisitorIdle, by the moment, their people's age and
        /// calling), now and then, never too often from one of them or on one map; said to <paramref name="to"/> when given.
        /// </summary>
        public static void Say(Pawn p, string moment, Pawn to = null)
        {
            if (p?.Map == null || moment == null || !Speech.On) return;
            try
            {
                int now = Now;
                int pawnLast = saidBy.TryGetValue(p.thingIDNumber, out int a) ? a : -1;
                int mapLast = saidOn.TryGetValue(p.Map.uniqueID, out int b) ? b : -1;
                if (!IdleHoursMath.MaySpeak(pawnLast, mapLast, now)) return;
                var tags = new List<string> { moment };
                if (p.Faction?.def != null) tags.Add(Peoples.TechOf(p.Faction) <= TechLevel.Medieval ? "theirLowtech" : "theirHightech");
                Peoples.AddTags(p.Faction, tags);
                if (p.DevelopmentalStage.Juvenile()) tags.Add("young");
                string line = Flavor.FromPool(Pool, p, null, null, tags.ToArray());
                if (line.NullOrEmpty()) return;
                // together: said to someone of the colony beside them; Speech.Say shows it only when the two are really together.
                bool said = to != null ? Speech.Say(p, line, to) : Speech.Say(p, line);
                if (!said) return;
                if (saidBy.Count > 300) saidBy.Clear();
                saidBy[p.thingIDNumber] = now;
                saidOn[p.Map.uniqueID] = now;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("visitors' idle words", e);
            }
        }

        // ---------------- Dev mode ----------------

        /// <summary>
        /// Each visitor on the map, their group, what they are at now, how much of their idle time this session has been
        /// filled, and what their idle hour could be now and why not (the dev tools' log); a line for the map first.
        /// </summary>
        public static List<string> Report(Map map)
        {
            Fresh();
            var lines = new List<string>();
            if (map == null) return lines;
            int up = 0, atStint = 0, standing = 0, sumStint = 0, sumIdle = 0;
            var rows = new List<string>();
            foreach (var p in map.mapPawns.AllPawnsSpawned.ToList())
            {
                if (p == null || !p.RaceProps.Humanlike || p.Faction == null || p.Faction.IsPlayer || p.HostileTo(Faction.OfPlayer)) continue;
                var now = p.jobs?.curDriver is JobDriver_VisitIdle vi ? vi.Doing.ToString() : p.CurJobDef?.defName ?? "-";
                if (p.Awake() && !p.Downed)
                {
                    up++;
                    if (p.jobs?.curDriver is JobDriver_VisitIdle) atStint++;
                    else if (p.CurJobDef == JobDefOf.GotoWander || p.CurJobDef == JobDefOf.Wait_Wander || p.CurJobDef == JobDefOf.StandAndBeSociallyActive) standing++;
                }
                string filled = "";
                if (tallies.TryGetValue(p.thingIDNumber, out var t))
                {
                    int stint = t.stint + (t.startedAt >= 0 ? Find.TickManager.TicksGame - t.startedAt : 0);
                    sumStint += stint;
                    sumIdle += t.idle;
                    filled = $"; filled {IdleHoursMath.FilledShare(stint, t.idle):P0} this session ({t.stints} stints, {stint / 2500f:0.0}h at them, {t.idle / 2500f:0.0}h standing about)";
                }
                var l = LookFor(p, out string why);
                if (l == null)
                {
                    rows.Add($"{p.LabelShort} ({p.GetLord()?.LordJob?.GetType().Name ?? "no group"}): now {now}{filled}; {why}");
                    continue;
                }
                var w = IdleHoursMath.Weights(l.facts);
                var fits = IdleHoursMath.All.Where(d => w[(int)d] > 0f).Select(d => d + " " + w[(int)d].ToString("0.0"));
                var f = l.facts;
                var has = new List<string>();
                if (f.ownBook) has.Add("their book");
                if (f.book) has.Add(l.books.Count + " of the colony's books");
                if (f.pet) has.Add(l.pets.Count + " pets");
                if (f.water) has.Add(l.fishSpots.Count + " fishing places");
                if (f.trainTarget) has.Add("a training target");
                if (f.bow) has.Add(f.archeryTarget ? "a bow and the archery target" : "a bow");
                if (f.table) has.Add("a table");
                if (l.walkEnds.Count > 0) has.Add(l.walkEnds.Count + " walk ends");
                if (f.worn) has.Add("worn " + l.worn.def.label);
                if (l.weapon != null) has.Add(l.weapon.def.label + " to see to");
                rows.Add($"{p.LabelShort}: {f.group}, {f.role}, age {f.age}{(f.child ? ", a child" : "")}{(f.titled ? ", titled" : "")}, {f.calling ?? "-"}, "
                         + $"{f.hour}h, {f.temperature:0}C{(f.rain ? ", rain" : "")}, reach {l.reach:0} of {l.focus}{(l.area != null ? " in their guest area" : "")}; now {now}{filled}; "
                         + (has.Count > 0 ? "has " + string.Join(", ", has) + "; " : "")
                         + (w.Any(x => x > 0f) ? "fits " + string.Join(", ", fits) : "nothing fits now"));
            }
            if (rows.Count == 0) return lines;
            lines.Add($"{up} up and about: {atStint} at something of their own, {standing} standing or wandering; this session {IdleHoursMath.FilledShare(sumStint, sumIdle):P0} of their idle time filled.");
            lines.AddRange(rows);
            return lines;
        }
    }
}
