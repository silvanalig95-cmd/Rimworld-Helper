using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// Nine more small habits, each the free-time moment a quirk promises (Defs/QuirkDefs/Council_Quirks_Habits.xml): one
    /// who can't stand a mess tidies a room at home; an early riser watches the sun come up; one who collects pretty stones
    /// looks for them by a stone chunk or the rock face; one who feeds the birds goes out to them; one who is always
    /// whittling, or sharpens every blade, sits down to it in a chair; one who knows every trail watches the tree line;
    /// one with steady hands looks in on someone in a sickbed; and one who remembers every birthday goes to wish whoever
    /// has one today a happy birthday.
    /// </summary>
    /// <remarks>
    /// The same safety as the others (<see cref="SmallHabits"/>), and only ever for a colonist who has the quirk (with the
    /// quirks setting on); arriving lives the habit (QuirkTracker.Notify_Lived). Nothing is used up or made: the crumbs,
    /// the shavings and the edge are only words. The one thing changed on the map is the mess a tidy one wipes up, a
    /// little at a time and the game's own way (Filth.ThinFilth). A patient looked in on, and one whose birthday was
    /// remembered, gets a small good memory. Nothing is computed per tick but the wiping, and nothing is saved.
    /// </remarks>
    public static class QuirkHabits
    {
        /// <summary>A tidy one wipes up a bit of the mess at their feet this often while they stay (ticks)...</summary>
        public const int TidyTicks = 120;
        /// <summary>...this many times at most in one while.</summary>
        public const int MaxTidied = 6;

        private static bool looked;
        private static QuirkDef tidy, earlyRiser, prettyStones, feedsBirds, whittler, sharpens, knowsTrails, steadyHands, birthdays;
        private static ThoughtDef lookedInOn, birthdayRemembered;

        private static void Look()
        {
            if (looked) return;
            looked = true;
            tidy = DefDatabase<QuirkDef>.GetNamedSilentFail("Council_Quirk_TidyTable");
            earlyRiser = DefDatabase<QuirkDef>.GetNamedSilentFail("Council_Quirk_EarlyRiser");
            prettyStones = DefDatabase<QuirkDef>.GetNamedSilentFail("Council_Quirk_PrettyStones");
            feedsBirds = DefDatabase<QuirkDef>.GetNamedSilentFail("Council_Quirk_FeedsBirds");
            whittler = DefDatabase<QuirkDef>.GetNamedSilentFail("Council_Quirk_Whittler");
            sharpens = DefDatabase<QuirkDef>.GetNamedSilentFail("Council_Quirk_SharpensBlades");
            knowsTrails = DefDatabase<QuirkDef>.GetNamedSilentFail("Council_Quirk_KnowsTrails");
            steadyHands = DefDatabase<QuirkDef>.GetNamedSilentFail("Council_Quirk_SteadyHands");
            birthdays = DefDatabase<QuirkDef>.GetNamedSilentFail("Council_Quirk_Birthdays");
            lookedInOn = DefDatabase<ThoughtDef>.GetNamedSilentFail("Council_Habit_LookedInOn");
            birthdayRemembered = DefDatabase<ThoughtDef>.GetNamedSilentFail("Council_Habit_BirthdayRemembered");
        }

        // ---------------- Theirs to have ----------------

        /// <summary>One who can't stand a mess, while there's some at home.</summary>
        public static bool TidyFor(Pawn p)
        {
            Look();
            return MoreHabits.HasQuirk(p, tidy) && p.Map.listerFilthInHomeArea.FilthInHomeArea.Count > 0;
        }

        /// <summary>An early riser, in the first hours of the day (four to seven).</summary>
        public static bool SunriseFor(Pawn p)
        {
            Look();
            int hour = GenLocalDate.HourOfDay(p.Map);
            return hour >= 4 && hour <= 7 && MoreHabits.HasQuirk(p, earlyRiser);
        }

        /// <summary>One who collects pretty stones, by day.</summary>
        public static bool StonesFor(Pawn p)
        {
            Look();
            return !VisualTouches.Dark(p.Map) && MoreHabits.HasQuirk(p, prettyStones);
        }

        /// <summary>One who feeds the birds, by day.</summary>
        public static bool BirdsFor(Pawn p)
        {
            Look();
            return !VisualTouches.Dark(p.Map) && MoreHabits.HasQuirk(p, feedsBirds);
        }

        /// <summary>One who is always whittling.</summary>
        public static bool WhittleFor(Pawn p)
        {
            Look();
            return MoreHabits.HasQuirk(p, whittler);
        }

        /// <summary>One who sharpens every blade.</summary>
        public static bool WhetstoneFor(Pawn p)
        {
            Look();
            return MoreHabits.HasQuirk(p, sharpens);
        }

        /// <summary>One who knows every trail, by day.</summary>
        public static bool TreeLineFor(Pawn p)
        {
            Look();
            return !VisualTouches.Dark(p.Map) && MoreHabits.HasQuirk(p, knowsTrails);
        }

        /// <summary>Steady hands, while someone here is awake in a sickbed and nobody has looked in on them lately.</summary>
        public static bool PatientFor(Pawn p)
        {
            Look();
            if (!MoreHabits.HasQuirk(p, steadyHands)) return false;
            foreach (var o in p.Map.mapPawns.FreeColonistsSpawned)
                if (o != p && NeedsLookingIn(o)) return true;
            return false;
        }

        /// <summary>One who remembers every birthday, while someone here has one today they haven't been wished a happy one for.</summary>
        public static bool BirthdayFor(Pawn p)
        {
            Look();
            if (!MoreHabits.HasQuirk(p, birthdays)) return false;
            foreach (var o in p.Map.mapPawns.FreeColonistsSpawned)
                if (o != p && BirthdayToday(o) && !Wished(o, p)) return true;
            return false;
        }

        // ---------------- Where ----------------

        /// <summary>
        /// The mess to tidy: filth in a roofed room of the home area (not a prison cell) within forty cells, the nearest
        /// they can reach, with a free cell beside it to stand on (<paramref name="cell"/>). Null when there's none.
        /// </summary>
        public static Thing FilthOf(Pawn p, out IntVec3 cell)
        {
            cell = IntVec3.Invalid;
            var map = p.Map;
            var home = map.areaManager?.Home;
            if (home == null) return null;
            var near = new List<Thing>();
            foreach (var f in map.listerFilthInHomeArea.FilthInHomeArea)
            {
                if (f == null || !f.Spawned || !f.Position.InHorDistOf(p.Position, 40f) || !home[f.Position] || !f.Position.Roofed(map) || f.IsForbidden(p)) continue;
                var room = f.Position.GetRoom(map);
                if (room == null || room.PsychologicallyOutdoors || room.IsPrisonCell) continue;
                near.Add(f);
            }
            foreach (var f in near.OrderBy(x => x.Position.DistanceToSquared(p.Position)).Take(6))
            {
                if (!p.CanReach(f, PathEndMode.Touch, Danger.None)) continue;
                var at = f.Position;
                var c = SmallHabits.SpotNear(at, map, p, x => x.AdjacentTo8WayOrInside(at));
                if (!c.IsValid) continue;
                cell = c;
                return f;
            }
            return null;
        }

        /// <summary>A bit of the mess within a step of them (in the home area) thinned once, as the game's cleaning does; true when there was some.</summary>
        public static bool WipeNear(Pawn p)
        {
            var map = p?.Map;
            var home = map?.areaManager?.Home;
            if (home == null) return false;
            foreach (var c in GenRadial.RadialCellsAround(p.Position, 1.5f, true))
            {
                if (!c.InBounds(map) || !home[c]) continue;
                var things = c.GetThingList(map);
                for (int i = 0; i < things.Count; i++)
                {
                    if (!(things[i] is Filth f) || f.Destroyed || f.IsForbidden(p)) continue;
                    f.ThinFilth();
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Somewhere to look for pretty stones within fifty cells: beside one of the nearer stone chunks, else at the foot of
        /// the natural rock, out in the open. What they look at (the chunk, the rock face) is <paramref name="at"/>.
        /// Invalid when there's nowhere.
        /// </summary>
        public static IntVec3 StonesSpot(Pawn p, out LocalTargetInfo at)
        {
            at = LocalTargetInfo.Invalid;
            var map = p.Map;
            bool outside = JoyUtility.EnjoyableOutsideNow(p);
            var chunks = new List<Thing>();
            foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.Chunk))
            {
                if (t == null || !t.Spawned || !t.def.IsWithinCategory(ThingCategoryDefOf.StoneChunks) || !t.Position.InHorDistOf(p.Position, 50f) || t.IsForbidden(p)) continue;
                if (!outside && !t.Position.Roofed(map)) continue;
                chunks.Add(t);
            }
            foreach (var t in chunks.OrderBy(x => x.Position.DistanceToSquared(p.Position)).Take(8).InRandomOrder())
            {
                var c = SmallHabits.SpotNear(t.Position, map, p, x => x.AdjacentTo8WayOrInside(t.Position));
                if (!c.IsValid) continue;
                at = t;
                return c;
            }
            if (!outside) return IntVec3.Invalid;
            var face = IntVec3.Invalid;
            bool Foot(IntVec3 c)
            {
                if (!c.InBounds(map) || !c.InHorDistOf(p.Position, 50f) || c.Roofed(map) || c.Fogged(map) || !c.Standable(map) || c.GetEdifice(map) != null) return false;
                for (int i = 0; i < 4; i++)
                {
                    var n = c + GenAdj.CardinalDirections[i];
                    if (!n.InBounds(map) || n.GetEdifice(map)?.def.building?.isNaturalRock != true) continue;
                    if (c.GetFirstPawn(map) != null || !map.pawnDestinationReservationManager.CanReserve(c, p) || !PlaySpots.Ok(c, map, p)) return false;
                    face = n;
                    return true;
                }
                return false;
            }
            if (!CellFinder.TryFindRandomCellNear(p.Position, map, 50, Foot, out var foot, 400)) return IntVec3.Invalid;
            at = face;
            return foot;
        }

        /// <summary>
        /// Where to feed the birds, under the open sky: beside a tree near the colony, else on open ground near its middle.
        /// What they face (the tree, or the ground ahead) is <paramref name="face"/>. Invalid when there's nowhere.
        /// </summary>
        public static IntVec3 BirdsSpot(Pawn p, out IntVec3 face)
        {
            face = IntVec3.Invalid;
            var map = p.Map;
            var center = TemplatePlanner.ColonyCenter(map);
            bool Tree(IntVec3 c) =>
                c.InBounds(map) && !c.Roofed(map) && !c.Fogged(map) && c.InHorDistOf(p.Position, 60f) && c.GetPlant(map) is Plant t && t.def.plant != null && t.def.plant.IsTree;
            if (CellFinder.TryFindRandomCellNear(center, map, 30, Tree, out var tree, 300))
            {
                var cell = SmallHabits.SpotNear(tree, map, p, x => !x.Roofed(map));
                if (cell.IsValid)
                {
                    face = tree;
                    return cell;
                }
            }
            bool Open(IntVec3 c) =>
                c.InBounds(map) && !c.Roofed(map) && !c.Fogged(map) && c.InHorDistOf(p.Position, 60f) && c.GetEdifice(map) == null && !(c.GetTerrain(map)?.IsWater ?? false);
            if (!CellFinder.TryFindRandomCellNear(center, map, 20, Open, out var open, 200)) return IntVec3.Invalid;
            var spot = SmallHabits.SpotNear(open, map, p, x => !x.Roofed(map));
            if (!spot.IsValid) return IntVec3.Invalid;
            face = (spot + Rot4.Random.FacingCell * 4).ClampInsideMap(map);
            return spot;
        }

        /// <summary>
        /// A free chair or bench to sit in within forty cells: one they can reserve and reach, not a throne, nor in a prison
        /// cell or someone else's bedroom; one indoors or by the colony's lit fire first. Out in the open and away from a
        /// fire, only in weather fit for it. Null when there's none.
        /// </summary>
        public static Thing SeatFor(Pawn p)
        {
            var map = p.Map;
            var fire = PlaySpots.ColonyFire(map, p);
            bool outside = JoyUtility.EnjoyableOutsideNow(p);
            Thing best = null;
            float bestScore = float.MinValue;
            foreach (var b in map.listerBuildings.allBuildingsColonist)
            {
                if (b == null || !b.Spawned || b is Building_Throne || b.def.building == null || !b.def.building.isSittable) continue;
                var c = b.Position;
                if (!c.InHorDistOf(p.Position, 40f) || b.IsForbidden(p)) continue;
                var room = c.GetRoom(map);
                if (room != null && (room.IsPrisonCell || SomeoneElses(room, p))) continue;
                bool roofed = c.Roofed(map);
                bool indoors = roofed && room != null && !room.PsychologicallyOutdoors;
                bool byFire = fire != null && c.InHorDistOf(fire.Position, 6f);
                if (!roofed && !outside) continue;
                float score = Rand.Value * 0.5f - c.DistanceTo(p.Position) / 40f + (indoors ? 1f : 0f) + (byFire ? 1f : 0f);
                if (score <= bestScore) continue;
                var there = c.GetFirstPawn(map);
                if (there != null && there != p) continue;
                if (!p.CanReserveSittableOrSpot(c) || !p.CanReach(c, PathEndMode.OnCell, Danger.None)) continue;
                best = b;
                bestScore = score;
            }
            return best;
        }

        /// <summary>A bedroom or barracks with owners, of whom they aren't one.</summary>
        private static bool SomeoneElses(Room room, Pawn p)
        {
            bool owned = false;
            foreach (var o in room.Owners)
            {
                if (o == p) return false;
                owned = true;
            }
            return owned;
        }

        // ---------------- The sick and the birthday ----------------

        /// <summary>Someone awake in a sickbed: in bed and in need of medical rest, or in a medical bed.</summary>
        public static bool StillPatient(Pawn o)
        {
            if (o == null || !o.Spawned || o.Dead || o.InMentalState || !o.Awake() || !o.InBed()) return false;
            return HealthAIUtility.ShouldSeekMedicalRest(o) || (o.CurrentBed()?.Medical ?? false);
        }

        /// <summary>A patient nobody has looked in on lately (the memory of it still with them).</summary>
        private static bool NeedsLookingIn(Pawn o) =>
            StillPatient(o) && (lookedInOn == null || o.needs?.mood?.thoughts?.memories?.GetFirstMemoryOfDef(lookedInOn) == null);

        /// <summary>Whom one with steady hands looks in on: a patient near them whose bed they can get to, the ones they like best first.</summary>
        public static Pawn PatientOf(Pawn p)
        {
            Look();
            Pawn best = null;
            float bestScore = float.MinValue;
            foreach (var o in p.Map.mapPawns.FreeColonistsSpawned)
            {
                if (o == p || !NeedsLookingIn(o) || !o.Position.InHorDistOf(p.Position, 50f)) continue;
                var bed = o.CurrentBed();
                if (bed == null || bed.IsForbidden(p)) continue;
                float score = Rand.Value;
                if (p.relations != null) score += Mathf.Clamp(p.relations.OpinionOf(o), -50, 50) / 50f;
                if (score <= bestScore || !p.CanReach(bed, PathEndMode.Touch, Danger.None)) continue;
                best = o;
                bestScore = score;
            }
            return best;
        }

        /// <summary>At the bedside, the patient awake and still in bed: glad someone came (a small good memory, held once).</summary>
        public static void LookedIn(Pawn by, Pawn patient)
        {
            Look();
            if (lookedInOn == null || by == null || !StillPatient(patient) || !by.Position.InHorDistOf(patient.Position, 3f)) return;
            CouncilGameComponent.GiveThought(patient, lookedInOn);
        }

        /// <summary>Today is their birthday, by the calendar's own reckoning (Calendar/CalendarTracker.cs: the day of the year they were born, where they are).</summary>
        public static bool BirthdayToday(Pawn o)
        {
            var map = o?.MapHeld;
            if (o?.ageTracker == null || map == null) return false;
            return CalendarMath.DaysUntil(GenLocalDate.DayOfYear(map), CalendarMath.Wrap(o.ageTracker.BirthDayOfYear)) == 0;
        }

        /// <summary>They already wished them a happy birthday (the memory of it lasts the day).</summary>
        private static bool Wished(Pawn whose, Pawn by)
        {
            var mem = whose.needs?.mood?.thoughts?.memories?.Memories;
            if (mem == null || birthdayRemembered == null) return false;
            foreach (var m in mem)
                if (m?.def == birthdayRemembered && m.otherPawn == by) return true;
            return false;
        }

        /// <summary>Whose birthday to remember: someone near whose birthday it is, at their leisure, not wished a happy one by them yet.</summary>
        public static Pawn BirthdayOf(Pawn p)
        {
            Look();
            Pawn best = null;
            float bestDist = float.MaxValue;
            foreach (var o in p.Map.mapPawns.FreeColonistsSpawned)
            {
                if (o == p || !BirthdayToday(o) || !o.Position.InHorDistOf(p.Position, 60f) || !MoreHabits.AtLeisure(o) || Wished(o, p)) continue;
                float d = o.Position.DistanceToSquared(p.Position);
                if (d >= bestDist || !p.CanReach(o, PathEndMode.Touch, Danger.None)) continue;
                best = o;
                bestDist = d;
            }
            return best;
        }

        /// <summary>A happy birthday wished face to face: the one whose birthday it is is glad to be remembered (a day's good mood, and a better opinion of them), once a birthday.</summary>
        public static void Remembered(Pawn by, Pawn whose)
        {
            Look();
            if (birthdayRemembered == null || !Together.Can(by, whose) || !BirthdayToday(whose) || Wished(whose, by)) return;
            CouncilGameComponent.GiveThought(whose, birthdayRemembered, by);
        }
    }

    // ---------------- The joy givers ----------------

    /// <summary>One who can't stand a mess: a while tidying a room at home, wiping a little of it up.</summary>
    public class JoyGiver_HabitTidy : JoyGiver_Habit
    {
        protected override HabitKind Kind => HabitKind.Tidy;

        protected override bool Fits(Pawn p) => QuirkHabits.TidyFor(p);

        protected override Job Give(Pawn p)
        {
            var filth = QuirkHabits.FilthOf(p, out var cell);
            return filth != null ? Make(cell, filth.Position, new IntRange(750, 1000)) : null;
        }
    }

    /// <summary>An early riser: out under the open sky at dawn, facing east, in weather fit for it.</summary>
    public class JoyGiver_HabitSunrise : JoyGiver_Habit
    {
        protected override HabitKind Kind => HabitKind.Sunrise;

        protected override bool Fits(Pawn p) => QuirkHabits.SunriseFor(p);

        protected override Job Give(Pawn p)
        {
            if (!JoyUtility.EnjoyableOutsideNow(p)) return null;
            var cell = MoreHabits.SkyCell(p);
            if (!cell.IsValid) return null;
            var east = (cell + IntVec3.East * 8).ClampInsideMap(p.Map);
            return Make(cell, east, new IntRange(1000, 1600));
        }
    }

    /// <summary>One who collects pretty stones: a while by a stone chunk or the rock face, by day.</summary>
    public class JoyGiver_HabitStones : JoyGiver_Habit
    {
        protected override HabitKind Kind => HabitKind.Stones;

        protected override bool Fits(Pawn p) => QuirkHabits.StonesFor(p);

        protected override Job Give(Pawn p)
        {
            var cell = QuirkHabits.StonesSpot(p, out var at);
            return cell.IsValid ? Make(cell, at, new IntRange(800, 1300)) : null;
        }
    }

    /// <summary>One who feeds the birds: a while out by a tree or on open ground near the colony, by day.</summary>
    public class JoyGiver_HabitBirds : JoyGiver_Habit
    {
        protected override HabitKind Kind => HabitKind.Birds;

        protected override bool Fits(Pawn p) => QuirkHabits.BirdsFor(p);

        protected override Job Give(Pawn p)
        {
            if (!JoyUtility.EnjoyableOutsideNow(p)) return null;
            var cell = QuirkHabits.BirdsSpot(p, out var face);
            return cell.IsValid ? Make(cell, face, new IntRange(800, 1300)) : null;
        }
    }

    /// <summary>One who is always whittling: a while in a chair, indoors or by the fire, with a knife and a bit of wood.</summary>
    public class JoyGiver_HabitWhittle : JoyGiver_Habit
    {
        protected override HabitKind Kind => HabitKind.Whittle;

        protected override bool Fits(Pawn p) => QuirkHabits.WhittleFor(p);

        protected override Job Give(Pawn p)
        {
            var seat = QuirkHabits.SeatFor(p);
            return seat != null ? Make(seat.Position, seat, new IntRange(1000, 1600)) : null;
        }
    }

    /// <summary>One who sharpens every blade: a while in a chair, indoors or by the fire, with a whetstone.</summary>
    public class JoyGiver_HabitWhetstone : JoyGiver_Habit
    {
        protected override HabitKind Kind => HabitKind.Whetstone;

        protected override bool Fits(Pawn p) => QuirkHabits.WhetstoneFor(p);

        protected override Job Give(Pawn p)
        {
            var seat = QuirkHabits.SeatFor(p);
            return seat != null ? Make(seat.Position, seat, new IntRange(900, 1400)) : null;
        }
    }

    /// <summary>One who knows every trail: a while just outside the colony, watching the tree line, by day.</summary>
    public class JoyGiver_HabitTreeLine : JoyGiver_Habit
    {
        protected override HabitKind Kind => HabitKind.TreeLine;

        protected override bool Fits(Pawn p) => QuirkHabits.TreeLineFor(p);

        protected override Job Give(Pawn p)
        {
            if (!JoyUtility.EnjoyableOutsideNow(p)) return null;
            var c = SmallHabits.LookOutSpot(p, out var face);
            return c.IsValid ? Make(c, face, new IntRange(1000, 1600)) : null;
        }
    }

    /// <summary>Steady hands: a while at the bedside of someone awake in a sickbed.</summary>
    public class JoyGiver_HabitPatient : JoyGiver_Habit
    {
        protected override HabitKind Kind => HabitKind.Patient;

        protected override bool Fits(Pawn p) => QuirkHabits.PatientFor(p);

        protected override Job Give(Pawn p)
        {
            var patient = QuirkHabits.PatientOf(p);
            if (patient == null) return null;
            var cell = SmallHabits.SpotNear(patient.Position, p.Map, p, x => x.InHorDistOf(patient.Position, 2.5f));
            return cell.IsValid ? Make(cell, patient, new IntRange(600, 1000)) : null;
        }
    }

    /// <summary>One who remembers every birthday: they find whoever has one today, at their leisure, and wish them a happy one.</summary>
    public class JoyGiver_HabitBirthday : JoyGiver_Habit
    {
        protected override HabitKind Kind => HabitKind.Birthday;

        protected override bool Fits(Pawn p) => QuirkHabits.BirthdayFor(p);

        protected override Job Give(Pawn p)
        {
            var other = QuirkHabits.BirthdayOf(p);
            if (other == null) return null;
            var cell = SmallHabits.SpotNear(other.Position, p.Map, p, x => x.AdjacentTo8WayOrInside(other.Position) || x.InHorDistOf(other.Position, 2.5f));
            return cell.IsValid ? Make(cell, other, new IntRange(600, 900)) : null;
        }
    }
}
