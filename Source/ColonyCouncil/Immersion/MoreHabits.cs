using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// Six more small habits, each from a feeling the heart knows or a quirk someone picked up: the awed (or one who
    /// watches the stars) out under the night sky; one who talks to the plants, by the crops; the tender (or one who
    /// names every animal) with one of the colony's animals; the worried (or one who counts everything) going over the
    /// food stores; the lonely keeping someone company, a Hospitality guest among them; and the nostalgic telling old
    /// stories by the evening fire.
    /// </summary>
    /// <remarks>
    /// The same safety as the first six (<see cref="SmallHabits"/>): joy givers, so only in free time and only for the
    /// colony's own, never at a party or in bed; each claims a cell of its own beside what it's about
    /// (<see cref="SmallHabits.SpotNear(IntVec3, Map, Pawn)"/>); the job ends at danger, an order, the setting turned off,
    /// full joy, or when the one they sit with walks off. A guest kept company stays in Hospitality's own lord: only the
    /// colonist goes to them. What's checked is already known (the heart's feelings, quirks, the stores' slot groups);
    /// nothing is computed per tick, and nothing is saved.
    /// </remarks>
    public static class MoreHabits
    {
        /// <summary>A feeling counts once it runs at least this strong (a third of full).</summary>
        public const float FeelingAt = 0.35f;

        private static bool looked;
        private static QuirkDef stargazer, talksToPlants, namesAnimals, countsThings;

        private static void Look()
        {
            if (looked) return;
            looked = true;
            stargazer = DefDatabase<QuirkDef>.GetNamedSilentFail("Council_Quirk_Stargazer");
            talksToPlants = DefDatabase<QuirkDef>.GetNamedSilentFail("Council_Quirk_TalksToPlants");
            namesAnimals = DefDatabase<QuirkDef>.GetNamedSilentFail("Council_Quirk_NamesAnimals");
            countsThings = DefDatabase<QuirkDef>.GetNamedSilentFail("Council_Quirk_CountsThings");
        }

        /// <summary>They have this quirk now (the quirks setting on).</summary>
        public static bool HasQuirk(Pawn p, QuirkDef q)
        {
            if (q == null || p == null || ColonyCouncilMod.Settings?.quirks != true) return false;
            return CouncilGameComponent.Instance?.quirks?.Get(p, q) != null;
        }

        /// <summary>The heart's feeling of this key, at least <see cref="FeelingAt"/> strong; null when it isn't.</summary>
        public static FeelingNow Feeling(Pawn p, string key, float at = FeelingAt)
        {
            var h = Heart.Of(p);
            if (h?.feelings == null) return null;
            foreach (var f in h.feelings)
                if (f?.def?.key == key && f.Share >= at) return f;
            return null;
        }

        private static bool Dark(Map map) => map != null && GenCelestial.CurCelestialSunGlow(map) < 0.3f;

        // ---------------- Theirs to have ----------------

        /// <summary>Awed, or one who watches the stars, on a clear night.</summary>
        public static bool StarsFor(Pawn p)
        {
            Look();
            return Dark(p.Map) && (HasQuirk(p, stargazer) || Feeling(p, "Awe") != null);
        }

        /// <summary>One who talks to the plants (or content, with a love of growing things), by day.</summary>
        public static bool PlantsFor(Pawn p)
        {
            Look();
            if (Dark(p.Map)) return false;
            if (HasQuirk(p, talksToPlants)) return true;
            var plants = p.skills?.GetSkill(SkillDefOf.Plants);
            return plants != null && !plants.TotallyDisabled && People.Passionate(plants) && Feeling(p, "Contentment", 0.5f) != null;
        }

        /// <summary>Tender, or one who names every animal, while the colony has an animal to sit with.</summary>
        public static bool AnimalsFor(Pawn p)
        {
            Look();
            if (p.Map?.mapPawns == null || p.Map.mapPawns.SpawnedColonyAnimals.Count == 0) return false;
            return HasQuirk(p, namesAnimals) || Feeling(p, "Tenderness") != null;
        }

        /// <summary>Worried, or one who counts everything.</summary>
        public static bool StoresFor(Pawn p)
        {
            Look();
            return HasQuirk(p, countsThings) || Feeling(p, "Worry") != null;
        }

        /// <summary>Lonely.</summary>
        public static bool CompanyFor(Pawn p) => Feeling(p, "Loneliness") != null;

        /// <summary>Nostalgic, of an evening, with the colony's fire lit.</summary>
        public static bool StoriesFor(Pawn p)
        {
            if (p.Map == null) return false;
            int hour = GenLocalDate.HourOfDay(p.Map);
            return hour >= 17 && hour <= 23 && Feeling(p, "Nostalgia") != null && PlaySpots.ColonyFire(p.Map, p) != null;
        }

        // ---------------- Whom and where ----------------

        /// <summary>
        /// Someone to keep company: a colonist at their leisure (as for <see cref="SmallHabits.CanListen"/>), or a
        /// Hospitality guest taking it easy (standing about, not at work or a meal). Nobody is asked to move: the lonely
        /// one goes to them.
        /// </summary>
        public static bool AtLeisure(Pawn o)
        {
            if (!StillGuest(o)) return SmallHabits.CanListen(o);
            if (o.pather != null && o.pather.Moving) return false;
            var job = o.CurJob;
            return job == null || (job.workGiverDef == null && job.def != JobDefOf.Ingest);
        }

        /// <summary>
        /// While they keep someone company: a colonist still at their leisure, or a guest still about and awake (a guest
        /// wanders as they please; if they wander off, the company simply ends).
        /// </summary>
        public static bool StillCompany(Pawn o) => StillGuest(o) || SmallHabits.CanListen(o);

        /// <summary>A Hospitality guest up and about, awake and able to talk.</summary>
        private static bool StillGuest(Pawn o)
        {
            if (o == null || !o.Spawned || o.Dead || o.Downed || o.Drafted || o.InMentalState || !o.Awake() || !Strangers.IsHospitalityGuest(o)) return false;
            return o.RaceProps?.Humanlike == true && !o.DevelopmentalStage.Baby() && o.health?.capacities != null && o.health.capacities.CapableOf(PawnCapacityDefOf.Talking);
        }

        /// <summary>Whom a lonely colonist keeps company: someone near, at their leisure, the ones they like best first.</summary>
        public static Pawn CompanyOf(Pawn p)
        {
            Pawn best = null;
            float bestScore = float.MinValue;
            var map = p.Map;
            void Consider(Pawn o, float bonus)
            {
                if (o == null || o == p || !o.Position.InHorDistOf(p.Position, 50f) || !AtLeisure(o)) return;
                float score = Rand.Value + bonus;
                if (p.relations != null) score += Mathf.Clamp(p.relations.OpinionOf(o), -50, 50) / 50f;
                if (score <= bestScore || !p.CanReach(o, PathEndMode.Touch, Danger.None)) return;
                best = o;
                bestScore = score;
            }
            foreach (var o in map.mapPawns.FreeColonistsSpawned) Consider(o, 0f);
            // A guest, now and then, if Hospitality is in: someone new to talk to.
            if (People.ModActive(Guests.Mod))
                foreach (var o in Strangers.On(map, StrangerKind.Visitor))
                    if (Strangers.IsHospitalityGuest(o)) Consider(o, 0.2f);
            return best;
        }

        /// <summary>An animal of the colony's that's calm and about: not downed, sleeping off a fight or in a state.</summary>
        public static bool Calm(Pawn a) =>
            a != null && a.Spawned && !a.Dead && !a.Downed && !a.InMentalState && a.Faction == Faction.OfPlayer && a.RaceProps?.Animal == true;

        /// <summary>The animal they sit with: their bonded one first, then the one the feeling is about, then any calm one near.</summary>
        public static Pawn AnimalFor(Pawn p)
        {
            var map = p.Map;
            bool Ok(Pawn a) => Calm(a) && a.Map == map && a.Position.InHorDistOf(p.Position, 60f) && !a.IsForbidden(p) && p.CanReach(a, PathEndMode.Touch, Danger.None);
            var bonded = p.relations?.GetFirstDirectRelationPawn(PawnRelationDefOf.Bond);
            if (Ok(bonded)) return bonded;
            var felt = Feeling(p, "Tenderness")?.target;
            if (felt?.RaceProps?.Animal == true && Ok(felt)) return felt;
            Pawn best = null;
            float bestScore = float.MinValue;
            foreach (var a in map.mapPawns.SpawnedColonyAnimals)
            {
                if (!Ok(a)) continue;
                float score = Rand.Value - a.Position.DistanceTo(p.Position) / 60f;
                if (a.playerSettings?.Master == p) score += 0.5f;
                if (score <= bestScore) continue;
                best = a;
                bestScore = score;
            }
            return best;
        }

        /// <summary>Something to go over in the colony's stores: food in the stockpile with the most of it, or null.</summary>
        public static Thing StoresOf(Pawn p)
        {
            var map = p.Map;
            Thing best = null;
            int bestCount = 0;
            foreach (var g in map.haulDestinationManager.AllGroupsListForReading)
            {
                if (g == null) continue;
                Thing first = null;
                int count = 0;
                foreach (var t in g.HeldThings)
                {
                    if (t?.def == null || !t.def.IsNutritionGivingIngestible || t.def.IsDrug) continue;
                    count += t.stackCount;
                    if (first == null && !t.IsForbidden(p)) first = t;
                }
                if (first == null || count <= bestCount || !first.Position.InHorDistOf(p.Position, 70f)) continue;
                best = first;
                bestCount = count;
            }
            return best;
        }

        /// <summary>A cell under the open sky for watching the stars, near where they are.</summary>
        public static IntVec3 SkyCell(Pawn p)
        {
            if (!RCellFinder.TryFindSkygazeCell(p.Position, p, out var c)) return IntVec3.Invalid;
            var map = p.Map;
            return SmallHabits.SpotNear(c, map, p, x => !x.Roofed(map));
        }

        /// <summary>A plant they could talk to: a growing crop in a field near them, or a plant in a pot.</summary>
        public static Thing PlantOf(Pawn p)
        {
            var map = p.Map;
            Thing best = null;
            float bestDist = 60f * 60f;
            foreach (var zone in map.zoneManager.AllZones)
            {
                if (!(zone is Zone_Growing field) || field.cells == null || field.cells.Count == 0) continue;
                var c = field.cells[Rand.Range(0, field.cells.Count)];
                var plant = c.GetPlant(map);
                if (plant == null || plant.LifeStage != PlantLifeStage.Growing && plant.LifeStage != PlantLifeStage.Mature) continue;
                float d = c.DistanceToSquared(p.Position);
                if (d >= bestDist) continue;
                best = plant;
                bestDist = d;
            }
            if (best != null) return best;
            // Built pots only (a pot still going up is a frame, and holds nothing).
            foreach (var b in Avail.ColonyBuildings(map, d => d.thingClass != null && typeof(Building_PlantGrower).IsAssignableFrom(d.thingClass)))
            {
                if (!(b is Building_PlantGrower pot) || !b.Spawned) continue;
                foreach (var plant in pot.PlantsOnMe)
                {
                    float d = b.Position.DistanceToSquared(p.Position);
                    if (d >= bestDist) continue;
                    best = plant;
                    bestDist = d;
                    break;
                }
            }
            return best;
        }
    }

    // ---------------- The joy givers ----------------

    /// <summary>Awed, or one who watches the stars: a while out under the night sky, in weather fit for it.</summary>
    public class JoyGiver_HabitStars : JoyGiver_Habit
    {
        protected override HabitKind Kind => HabitKind.Stars;

        protected override bool Fits(Pawn p) => MoreHabits.StarsFor(p);

        protected override Job Give(Pawn p)
        {
            if (!JoyUtility.EnjoyableOutsideNow(p)) return null;
            var cell = MoreHabits.SkyCell(p);
            return cell.IsValid ? Make(cell, LocalTargetInfo.Invalid, new IntRange(1200, 2000)) : null;
        }
    }

    /// <summary>One who talks to the plants: a while beside a crop or a potted plant, talking to it.</summary>
    public class JoyGiver_HabitPlants : JoyGiver_Habit
    {
        protected override HabitKind Kind => HabitKind.Plants;

        protected override bool Fits(Pawn p) => MoreHabits.PlantsFor(p);

        protected override Job Give(Pawn p)
        {
            var plant = MoreHabits.PlantOf(p);
            if (plant == null || (!plant.Position.Roofed(p.Map) && !JoyUtility.EnjoyableOutsideNow(p))) return null;
            var cell = SmallHabits.SpotNear(plant.Position, p.Map, p);
            return cell.IsValid ? Make(cell, plant, new IntRange(900, 1500)) : null;
        }
    }

    /// <summary>Tender, or one who names every animal: a while beside one of the colony's animals.</summary>
    public class JoyGiver_HabitAnimals : JoyGiver_Habit
    {
        protected override HabitKind Kind => HabitKind.Animals;

        protected override bool Fits(Pawn p) => MoreHabits.AnimalsFor(p);

        protected override Job Give(Pawn p)
        {
            var animal = MoreHabits.AnimalFor(p);
            if (animal == null || (!animal.Position.Roofed(p.Map) && !JoyUtility.EnjoyableOutsideNow(p))) return null;
            var cell = SmallHabits.SpotNear(animal.Position, p.Map, p);
            return cell.IsValid ? Make(cell, animal, new IntRange(800, 1400)) : null;
        }
    }

    /// <summary>Worried, or one who counts everything: a while going over the food stores.</summary>
    public class JoyGiver_HabitStores : JoyGiver_Habit
    {
        protected override HabitKind Kind => HabitKind.Stores;

        protected override bool Fits(Pawn p) => MoreHabits.StoresFor(p);

        protected override Job Give(Pawn p)
        {
            var food = MoreHabits.StoresOf(p);
            if (food == null) return null;
            var cell = SmallHabits.SpotNear(food.Position, p.Map, p);
            return cell.IsValid ? Make(cell, food, new IntRange(700, 1200)) : null;
        }
    }

    /// <summary>Lonely: they find someone at their leisure, a guest among them, and keep them company a while.</summary>
    public class JoyGiver_HabitCompany : JoyGiver_Habit
    {
        protected override HabitKind Kind => HabitKind.Company;

        protected override bool Fits(Pawn p) => MoreHabits.CompanyFor(p);

        protected override Job Give(Pawn p)
        {
            var other = MoreHabits.CompanyOf(p);
            if (other == null) return null;
            var cell = SmallHabits.SpotNear(other.Position, p.Map, p, x => x.AdjacentTo8WayOrInside(other.Position) || x.InHorDistOf(other.Position, 2.5f));
            return cell.IsValid ? Make(cell, other, new IntRange(800, 1300)) : null;
        }
    }

    /// <summary>Nostalgic, of an evening: a while by the colony's lit fire, telling old stories to whoever sits there.</summary>
    public class JoyGiver_HabitStories : JoyGiver_Habit
    {
        protected override HabitKind Kind => HabitKind.Stories;

        protected override bool Fits(Pawn p) => MoreHabits.StoriesFor(p);

        protected override Job Give(Pawn p)
        {
            var fire = PlaySpots.ColonyFire(p.Map, p);
            if (fire == null || fire.IsForbidden(p) || !fire.Position.InHorDistOf(p.Position, 80f) || !p.CanReach(fire, PathEndMode.Touch, Danger.None)) return null;
            var cell = SmallHabits.SpotNear(fire.Position, p.Map, p);
            return cell.IsValid ? Make(cell, fire, new IntRange(1000, 1800)) : null;
        }
    }
}
