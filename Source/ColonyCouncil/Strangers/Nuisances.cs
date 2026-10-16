using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>What on the map a nuisance needs, checked for the colonist it would happen to.</summary>
    public enum NuisanceCheck
    {
        /// <summary>A door of ours near them, worn below 70% of its hit points.</summary>
        StuckDoor,
        /// <summary>Rain or snow, and their bedroom or the room they're in has a gap in its roof.</summary>
        LeakyRoof,
        /// <summary>The room they're in, if it isn't cold, holds food close to spoiling or an animal carcass that's rotting.</summary>
        SpoilingFood,
        /// <summary>Their bedroom is tiny for the beds in it.</summary>
        CrampedBedroom,
        /// <summary>A running generator that makes noise, a few steps from their bed.</summary>
        NoisyGenerator,
        /// <summary>A wood fire (a campfire, a brazier) burning in the small closed room they're in.</summary>
        SmokyRoom,
        /// <summary>Their bed is a long way from the nearest table to eat at.</summary>
        LongWalk,
        /// <summary>A swamp or jungle, warm out, and they're outside.</summary>
        Insects,
        /// <summary>Animal filth right where they're standing.</summary>
        AnimalFilth,
        /// <summary>Things lying around all over the map, waiting to be hauled.</summary>
        Clutter,
        /// <summary>They're wearing something of awful or poor quality.</summary>
        ShoddyClothes,
        /// <summary>They sleep on a bedroll, a sleeping spot or a bed with little comfort.</summary>
        HardBed,
        /// <summary>The closed room they're in is filthy.</summary>
        DirtyRoom,
        /// <summary>The closed room they're in is below 12 °C (not a freezer) while it's cold outside.</summary>
        ChillyRoom,
        /// <summary>The closed room they're in is above 29 °C.</summary>
        StuffyRoom,
        /// <summary>One of the colony's animals is in their bedroom with them ({THING} is the animal).</summary>
        PetInBed,
        /// <summary>Something of ours is being built a few steps from their bed ({THING} is what).</summary>
        BuildingWork,
        /// <summary>A small wild animal is in the room with them ({THING} is its kind).</summary>
        Vermin,
        /// <summary>Their bed stands on bare earth.</summary>
        BareFloor
    }

    /// <summary>
    /// A small irritation that comes from the colony itself rather than from another colonist: a door that sticks, a
    /// leaking roof, smoke in a small room, a generator droning by a bed. The encounter system's solo category (one
    /// colonist, no stranger). Each check reads the real map, so a well-kept, roomy colony rarely has them and a messy
    /// or cramped one has them often. Texts: {A_...} the colonist, {THING} the thing in question (a door, a shirt, a
    /// generator); a line may carry flavor tags ([winter], [night]) and is preferred when they fit.
    /// </summary>
    public class NuisanceDef : Def
    {
        public NuisanceCheck check;
        /// <summary>A check of its own (a <see cref="NuisanceCheckWorker"/>), used instead of <see cref="check"/>.</summary>
        public Type checkClass;
        /// <summary>Who it can happen to, and when, as flavor tags: "morning|evening", "!trait:Ascetic".</summary>
        public string who;
        public bool children;
        public float weight = 1f;
        /// <summary>The same colonist won't have this one again for this long.</summary>
        public float cooldownDays = 4f;
        public ThoughtDef thought;
        [MustTranslate] public List<string> texts = new List<string>();

        [Unsaved] private string whoExpr;
        [Unsaved] private NuisanceCheckWorker worker;

        public string WhoExpr => whoExpr ??= "[" + who + "]";

        public NuisanceCheckWorker Worker
        {
            get
            {
                if (worker == null && checkClass != null) worker = (NuisanceCheckWorker)Activator.CreateInstance(checkClass);
                return worker;
            }
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            if (thought == null) yield return "nuisance has no thought";
            if (checkClass != null && !typeof(NuisanceCheckWorker).IsAssignableFrom(checkClass)) yield return "checkClass must be a NuisanceCheckWorker";
            if (texts.NullOrEmpty()) yield return "nuisance has no texts";
            else if (texts.All(t => Flavor.Strip(t, out var tags) != null && tags != null && tags.Length > 0)) yield return "nuisance needs at least one untagged text";
        }
    }

    /// <summary>
    /// Runs the nuisances, from the encounter tracker's hourly turn: now and then one colonist at home has the one that
    /// fits the map right now. At most one every half day per colonist, and the same one not again for some days.
    /// </summary>
    public class NuisanceTracker : IExposable
    {
        // "ThingID|defName": when they last had that one; "ThingID|any": when they last had any.
        private Dictionary<string, int> last = new Dictionary<string, int>();
        private const int GapTicks = GenDate.TicksPerHour * 12;
        private const int MaxThingsLooked = 400;

        public void Hourly()
        {
            var s = ColonyCouncilMod.Settings;
            if (!s.frictions) return;
            int now = Find.TickManager.TicksGame;
            if (now < GenDate.TicksPerDay * 2) return;
            foreach (var map in Find.Maps)
            {
                if (!map.IsPlayerHome || MapFacts.ThreatOn(map)) continue;
                var colonists = map.mapPawns.FreeColonistsSpawned
                    .Where(p => !p.IsQuestLodger() && People.HasInnerLife(p) && !p.Downed && !p.InMentalState && !p.Drafted && p.Awake() && !p.DevelopmentalStage.Baby())
                    .ToList();
                if (colonists.Count == 0) continue;
                // About one try a day per colonist; whether anything comes of it depends on the state of the place.
                float chance = Mathf.Min(0.5f, 0.04f * colonists.Count) * Mathf.Clamp(s.momentFrequency, 0.1f, 4f);
                if (!Rand.Chance(chance)) continue;
                var p = colonists.RandomElement();
                if (last.TryGetValue(p.ThingID + "|any", out int any) && any <= now && now - any < GapTicks) continue;
                TryOne(p, map, now);
            }
        }

        /// <summary>The first nuisance that fits this colonist now (by weight, with some luck), if any.</summary>
        public bool TryOne(Pawn p, Map map, int now, Func<NuisanceDef, bool> only = null)
        {
            var tags = Flavor.TagsFor(p);
            bool child = p.DevelopmentalStage.Juvenile();
            foreach (var def in DefDatabase<NuisanceDef>.AllDefsListForReading.InRandomOrder().OrderByDescending(d => d.weight * Rand.Value))
            {
                if ((child && !def.children) || (only != null && !only(def))) continue;
                if (last.TryGetValue(p.ThingID + "|" + def.defName, out int t) && t <= now && now - t < def.cooldownDays * GenDate.TicksPerDay) continue;
                if (!def.who.NullOrEmpty() && Flavor.Match(def.WhoExpr, tags) < 0) continue;
                string thing;
                try
                {
                    thing = def.checkClass != null ? def.Worker.Check(p, map) : Check(def.check, p, map);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("nuisance " + def.defName, e);
                    continue;
                }
                if (thing == null) continue;
                Fire(def, p, thing, tags);
                last[p.ThingID + "|" + def.defName] = now;
                last[p.ThingID + "|any"] = now;
                return true;
            }
            return false;
        }

        private static void Fire(NuisanceDef def, Pawn p, string thing, HashSet<string> tags)
        {
            string raw = Flavor.PickFresh(def.texts, tags, def.texts) ?? Flavor.PickText(def.texts);
            if (raw.NullOrEmpty()) return;
            string text = raw.Formatted(p.Named("A"), thing.Named("THING")).Resolve();
            CouncilGameComponent.GiveThought(p, def.thought, null, 1f, text);
            News.Post(text, new LookTargets(p), NewsKind.Colony, MessageTypeDefOf.SilentInput, Importance.Flavor);
        }

        /// <summary>The thing it's about (its label), "" when it's about no one thing, or null when it doesn't fit.</summary>
        private static string Check(NuisanceCheck check, Pawn p, Map map)
        {
            switch (check)
            {
                case NuisanceCheck.StuckDoor: return StuckDoor(p, map);
                case NuisanceCheck.LeakyRoof: return LeakyRoof(p, map);
                case NuisanceCheck.SpoilingFood: return SpoilingFood(p);
                case NuisanceCheck.CrampedBedroom: return CrampedBedroom(p);
                case NuisanceCheck.NoisyGenerator: return NoisyGenerator(p, map);
                case NuisanceCheck.SmokyRoom: return SmokyRoom(p);
                case NuisanceCheck.LongWalk: return LongWalk(p, map);
                case NuisanceCheck.Insects: return Insects(p, map);
                case NuisanceCheck.AnimalFilth: return AnimalFilth(p, map);
                case NuisanceCheck.Clutter: return Clutter(map);
                case NuisanceCheck.ShoddyClothes: return ShoddyClothes(p);
                case NuisanceCheck.HardBed: return HardBed(p);
                case NuisanceCheck.DirtyRoom: return DirtyRoom(p);
                case NuisanceCheck.ChillyRoom: return ChillyRoom(p, map);
                case NuisanceCheck.StuffyRoom: return StuffyRoom(p);
                case NuisanceCheck.PetInBed: return PetInBed(p);
                case NuisanceCheck.BuildingWork: return BuildingWork(p, map);
                case NuisanceCheck.Vermin: return Vermin(p, map);
                case NuisanceCheck.BareFloor: return BareFloor(p, map);
            }
            return null;
        }

        /// <summary>The closed room they're in, or null outdoors.</summary>
        private static Room IndoorRoom(Pawn p)
        {
            var room = p.GetRoom();
            return room != null && room.ProperRoom && !room.PsychologicallyOutdoors ? room : null;
        }

        private static Building_Bed Bed(Pawn p, Map map)
        {
            var bed = p.ownership?.OwnedBed;
            return bed != null && bed.Spawned && bed.Map == map ? bed : null;
        }

        private static string StuckDoor(Pawn p, Map map)
        {
            foreach (var t in GenRadial.RadialDistinctThingsAround(p.Position, map, 6f, true))
                if (t is Building_Door door && door.def.useHitPoints && door.Faction == Faction.OfPlayer && door.HitPoints < door.MaxHitPoints * 0.7f)
                    return door.def.label;
            return null;
        }

        private static string LeakyRoof(Pawn p, Map map)
        {
            var weather = map.weatherManager?.curWeather;
            if (weather == null || (weather.rainRate <= 0.01f && weather.snowRate <= 0.01f)) return null;
            foreach (var room in new[] { RoomUtil.BedRoom(p), IndoorRoom(p) })
                if (room != null && room.ProperRoom && !room.PsychologicallyOutdoors && room.OpenRoofCount > 0) return "";
            return null;
        }

        private static string SpoilingFood(Pawn p)
        {
            var room = IndoorRoom(p);
            if (room == null || room.Temperature < 4f) return null;
            var things = room.ContainedAndAdjacentThings;
            for (int i = 0; i < things.Count && i < MaxThingsLooked; i++)
            {
                var t = things[i];
                if (t is Corpse corpse)
                {
                    // The game already minds people's corpses; this is the butcher's backlog.
                    if (corpse.InnerPawn != null && !corpse.InnerPawn.RaceProps.Humanlike && corpse.GetRoom() == room
                        && corpse.TryGetComp<CompRottable>()?.Stage == RotStage.Rotting) return corpse.LabelNoCount;
                    continue;
                }
                if (t.def.category != ThingCategory.Item || !t.def.IsIngestible || t.GetRoom() != room) continue;
                var rot = t.TryGetComp<CompRottable>();
                if (rot != null && rot.PropsRot.TicksToRotStart > 0 && rot.RotProgressPct > 0.8f) return t.def.label;
            }
            return null;
        }

        private static string CrampedBedroom(Pawn p)
        {
            var room = RoomUtil.BedRoom(p);
            if (room == null || !room.ProperRoom || room.PsychologicallyOutdoors) return null;
            int slots = room.ContainedBeds.Sum(b => b.SleepingSlotsCount);
            if (slots <= 0) return null;
            return room.GetStat(RoomStatDefOf.Space) / slots < 10f ? "" : null;
        }

        private static string NoisyGenerator(Pawn p, Map map)
        {
            var bed = Bed(p, map);
            if (bed == null) return null;
            foreach (var t in GenRadial.RadialDistinctThingsAround(bed.Position, map, 5.9f, true))
            {
                if (t.def.category != ThingCategory.Building || t.def.GetCompProperties<CompProperties_Power>()?.soundAmbientProducingPower == null) continue;
                var power = t.TryGetComp<CompPowerTrader>();
                if (power != null && power.PowerOutput > 0f) return t.def.label;
            }
            return null;
        }

        private static string SmokyRoom(Pawn p)
        {
            var room = IndoorRoom(p);
            if (room == null || room.CellCount > 60) return null;
            foreach (var t in room.ContainedAndAdjacentThings)
            {
                if (t.def.category != ThingCategory.Building || t.GetRoom() != room) continue;
                var heat = t.def.GetCompProperties<CompProperties_HeatPusher>();
                if (heat == null || heat.heatPerSecond < 10f || t.def.GetCompProperties<CompProperties_Glower>() == null) continue;
                var fuel = t.TryGetComp<CompRefuelable>();
                if (fuel != null && fuel.HasFuel) return t.def.label;
            }
            return null;
        }

        private static string LongWalk(Pawn p, Map map)
        {
            var bed = Bed(p, map);
            if (bed == null) return null;
            int best = int.MaxValue;
            foreach (var b in map.listerBuildings.allBuildingsColonist)
            {
                if (b.def.surfaceType != SurfaceType.Eat) continue;
                int d = (b.Position - bed.Position).LengthHorizontalSquared;
                if (d < best) best = d;
            }
            // No table at all is the game's own "ate without a table".
            return best != int.MaxValue && best > 50 * 50 ? "" : null;
        }

        private static readonly string[] WetBiomes = { "swamp", "jungle", "rainforest", "bog", "marsh", "wetland", "mangrove" };

        private static string Insects(Pawn p, Map map)
        {
            string biome = map.Biome?.defName?.ToLowerInvariant();
            if (biome == null || !WetBiomes.Any(biome.Contains) || map.mapTemperature.OutdoorTemp < 18f) return null;
            var room = p.GetRoom();
            return room == null || room.PsychologicallyOutdoors ? "" : null;
        }

        private static string AnimalFilth(Pawn p, Map map)
        {
            foreach (var t in GenRadial.RadialDistinctThingsAround(p.Position, map, 1.5f, true))
                if (t.def.category == ThingCategory.Filth && t.def.defName.IndexOf("Animal", StringComparison.OrdinalIgnoreCase) >= 0) return "";
            return null;
        }

        private static string Clutter(Map map) => (map.listerHaulables?.ThingsPotentiallyNeedingHauling()?.Count ?? 0) >= 50 ? "" : null;

        private static string ShoddyClothes(Pawn p)
        {
            var worn = p.apparel?.WornApparel;
            if (worn == null) return null;
            foreach (var ap in worn)
                if (ap.TryGetQuality(out var q) && q <= QualityCategory.Poor) return ap.def.label;
            return null;
        }

        private static string HardBed(Pawn p)
        {
            var bed = p.ownership?.OwnedBed;
            if (bed == null || !bed.Spawned) return null;
            string n = bed.def.defName;
            bool hard = bed.GetStatValue(StatDefOf.Comfort) < 0.6f || n.IndexOf("Bedroll", StringComparison.OrdinalIgnoreCase) >= 0
                        || n.IndexOf("SleepingSpot", StringComparison.OrdinalIgnoreCase) >= 0;
            return hard ? bed.def.label : null;
        }

        private static string DirtyRoom(Pawn p)
        {
            var room = IndoorRoom(p);
            return room != null && room.GetStat(RoomStatDefOf.Cleanliness) < -1f ? "" : null;
        }

        private static string ChillyRoom(Pawn p, Map map)
        {
            var room = IndoorRoom(p);
            if (room == null || map.mapTemperature.OutdoorTemp > 5f) return null;
            float t = room.Temperature;
            // Below freezing is a freezer or a lost cause, not a draft.
            return t >= 0f && t < 12f ? "" : null;
        }

        private static string StuffyRoom(Pawn p)
        {
            var room = IndoorRoom(p);
            return room != null && room.Temperature > 29f ? "" : null;
        }

        /// <summary>A colony animal in their own bedroom (not a barracks), while they're in it.</summary>
        private static string PetInBed(Pawn p)
        {
            var room = RoomUtil.BedRoom(p);
            if (room == null || !room.ProperRoom || room.PsychologicallyOutdoors || p.GetRoom() != room) return null;
            if (room.ContainedBeds.Sum(b => b.SleepingSlotsCount) > 2) return null;
            var things = room.ContainedAndAdjacentThings;
            for (int i = 0; i < things.Count && i < MaxThingsLooked; i++)
                if (things[i] is Pawn animal && animal != p && animal.RaceProps.Animal && animal.Faction == Faction.OfPlayer && !animal.Dead && animal.GetRoom() == room)
                    return animal.LabelShort;
            return null;
        }

        /// <summary>Building work of ours (a frame being put up) a few steps from their bed.</summary>
        private static string BuildingWork(Pawn p, Map map)
        {
            var bed = Bed(p, map);
            if (bed == null) return null;
            foreach (var t in GenRadial.RadialDistinctThingsAround(bed.Position, map, 6f, true))
                if (t is Frame frame && frame.Faction == Faction.OfPlayer && frame.def.entityDefToBuild != null)
                    return frame.def.entityDefToBuild.label;
            return null;
        }

        /// <summary>A small wild animal (a rat, a squirrel) in the closed room they're in.</summary>
        private static string Vermin(Pawn p, Map map)
        {
            var room = IndoorRoom(p);
            if (room == null || !map.IsPlayerHome) return null;
            foreach (var t in GenRadial.RadialDistinctThingsAround(p.Position, map, 9f, true))
            {
                if (!(t is Pawn animal) || animal.Dead || animal.Faction != null || animal.RaceProps == null || !animal.RaceProps.Animal) continue;
                if (animal.BodySize > 0.4f || animal.RaceProps.FleshType == FleshTypeDefOf.Insectoid || animal.GetRoom() != room) continue;
                return animal.KindLabel;
            }
            return null;
        }

        /// <summary>Their bed stands on bare earth (ground plants could grow in): no floor laid.</summary>
        private static string BareFloor(Pawn p, Map map)
        {
            var bed = Bed(p, map);
            if (bed == null) return null;
            var room = bed.GetRoom();
            if (room == null || !room.ProperRoom || room.PsychologicallyOutdoors) return null;
            var terrain = bed.Position.GetTerrain(map);
            return terrain != null && terrain.fertility > 0f ? "" : null;
        }

        public void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                int now = Find.TickManager.TicksGame;
                last.RemoveAll(kv => now - kv.Value > GenDate.TicksPerDay * 10);
                CouncilGameComponent.PruneByPawn(last);
            }
            Scribe_Collections.Look(ref last, "last", LookMode.Value, LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && last == null) last = new Dictionary<string, int>();
        }
    }
}
