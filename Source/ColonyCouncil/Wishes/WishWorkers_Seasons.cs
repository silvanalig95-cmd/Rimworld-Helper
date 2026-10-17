using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    // The wishes of the land's year (Defs/WishDefs/Wishes_Seasons.xml and the reworked ones beside them): clothes that keep
    // someone comfortable through the season coming, warm or cool, and a sauna on cold land, with the small habit of
    // using one (Defs/Misc/Rimfolk_SmallHabits_Sauna.xml). The arithmetic is in SeasonWishMath.

    /// <summary>
    /// Clothes for the season: someone whose clothes leave them uncomfortable in the coming season's usual cold (or, on
    /// hot land, its heat) wants something to wear that covers it. Not a coat to keep in store: what they wear has to keep
    /// them comfortable at the season's worst, as near as one new piece the colony has or can make can take them.
    /// Fulfilled when their comfortable range (the game's own, from what they wear) reaches it; let go quietly when the
    /// season has passed. Only what their apparel policy, their beliefs (no clothes for a nudist, no leather for one who
    /// minds it) and their body let them wear counts, and nothing locked on them (a role's apparel) is ever replaced.
    /// </summary>
    public abstract class WishWorker_SeasonClothes : WishWorker
    {
        /// <summary>For the heat (a cooler hat, a duster) rather than the cold.</summary>
        protected abstract bool Heat { get; }

        private StatDef Comfy => Heat ? StatDefOf.ComfyTemperatureMax : StatDefOf.ComfyTemperatureMin;

        private StatDef Insulation => Heat ? StatDefOf.Insulation_Heat : StatDefOf.Insulation_Cold;

        /// <summary>The heat is only asked about when the season's afternoons come to this or more (°C).</summary>
        public const float HotSeason = 32f;
        /// <summary>The cold only when the season's evenings come to this or less (°C).</summary>
        public const float ColdSeason = 10f;

        // ---------------- The season ----------------

        private static readonly Dictionary<int, (int day, float[] temps)> averages = new Dictionary<int, (int, float[])>();

        /// <summary>The year's twelve twelfths at this map, worked out once a day.</summary>
        public static float[] Averages(Map map)
        {
            int day = Find.TickManager.TicksGame / GenDate.TicksPerDay;
            if (averages.TryGetValue(map.uniqueID, out var c) && c.day == day) return c.temps;
            var t = new float[12];
            for (int i = 0; i < 12; i++) t[i] = SeasonUtil.TwelfthTemp(map, (Twelfth)i);
            if (averages.Count > 20) averages.Clear();
            averages[map.uniqueID] = (day, t);
            return t;
        }

        public static float SeasonTemp(Map map, bool heat) => SeasonWishMath.SeasonTemp(Averages(map), (int)GenLocalDate.Twelfth(map), heat);

        /// <summary>What the season is called in the wish ("the winter ahead", "the summer").</summary>
        private string SeasonLabel(Map map)
        {
            var s = GenLocalDate.Season(map);
            if (Heat)
            {
                if (s == Season.Summer || s == Season.PermanentSummer) return "CC_Season_Summer".Translate();
                return s == Season.Spring ? "CC_Season_SummerAhead".Translate() : "CC_Season_Hot".Translate();
            }
            if (s == Season.Winter || s == Season.PermanentWinter) return "CC_Season_Winter".Translate();
            return s == Season.Fall ? "CC_Season_WinterAhead".Translate() : "CC_Season_Cold".Translate();
        }

        // ---------------- What they could wear ----------------

        /// <summary>Theirs to wear: the right body and age, their apparel policy, their beliefs (Wishes/Beliefs.cs).</summary>
        private static bool MayWear(Pawn p, ThingDef d)
        {
            if (d?.apparel == null || p?.apparel == null) return false;
            if (!d.apparel.PawnCanWear(p) || !ApparelUtility.HasPartsToWear(p, d)) return false;
            if (Beliefs.AgainstThing(p, d)) return false;
            var policy = p.outfits?.CurrentApparelPolicy;
            return policy?.filter == null || policy.filter.Allows(d);
        }

        /// <summary>
        /// What a piece adds over what it would replace on them (the worn things it can't be worn with); -1 when it would
        /// take off something locked on them.
        /// </summary>
        private float Gain(Pawn p, ThingDef d, float insulation)
        {
            float lost = 0f;
            var worn = p.apparel.WornApparel;
            var body = p.RaceProps?.body;
            for (int i = 0; i < worn.Count; i++)
            {
                var w = worn[i];
                if (w == null || ApparelUtility.CanWearTogether(d, w.def, body)) continue;
                if (p.apparel.IsLocked(w)) return -1f;
                lost += w.GetStatValue(Insulation);
            }
            return insulation - lost;
        }

        private static readonly Dictionary<int, (int hour, List<ThingDef> defs)> makeable = new Dictionary<int, (int, List<ThingDef>)>();

        /// <summary>Clothes the colony can make here now (the research done, a bench it has or can build), worked out once an hour.</summary>
        private static List<ThingDef> Makeable(Map map)
        {
            int hour = Find.TickManager.TicksGame / GenDate.TicksPerHour;
            if (makeable.TryGetValue(map.uniqueID, out var c) && c.hour == hour) return c.defs;
            var list = new List<ThingDef>();
            try
            {
                foreach (var r in DefDatabase<RecipeDef>.AllDefsListForReading)
                {
                    var d = r.ProducedThingDef;
                    if (d?.apparel == null || list.Contains(d) || !r.AvailableNow) continue;
                    if (Avail.RecipeUsers(r).Any(u => map.listerBuildings.ColonistsHaveBuilding(u) || Avail.CanBuild(u, map))) list.Add(d);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("season clothes: what can be made", e);
            }
            if (makeable.Count > 20) makeable.Clear();
            makeable[map.uniqueID] = (hour, list);
            return list;
        }

        /// <summary>
        /// The piece that would do the most for them: one in the colony's stores (not a dead man's, not leather for one who
        /// minds it), or one the colony can make. Null when nothing helps.
        /// </summary>
        private ThingDef BestPiece(Pawn p, Map map, out float gain, out Apparel inStore)
        {
            gain = 0f;
            inStore = null;
            ThingDef best = null;
            bool noLeather = Beliefs.Forbids(p, Belief.NoLeather);
            var home = map.areaManager.Home;
            foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.Apparel))
            {
                if (!(t is Apparel a) || !a.Spawned || a.WornByCorpse || a.IsForbidden(Faction.OfPlayer) || (!a.IsInAnyStorage() && !home[a.Position])) continue;
                if (noLeather && a.Stuff != null && ThingCategoryDefOf.Leathers != null && a.Stuff.IsWithinCategory(ThingCategoryDefOf.Leathers)) continue;
                if (!MayWear(p, a.def)) continue;
                float g = Gain(p, a.def, a.GetStatValue(Insulation));
                if (g <= gain) continue;
                gain = g;
                best = a.def;
                inStore = a;
            }
            foreach (var d in Makeable(map))
            {
                if (!MayWear(p, d)) continue;
                var stuff = d.MadeFromStuff ? GenStuff.DefaultStuffFor(d) : null;
                float g = Gain(p, d, d.GetStatValueAbstract(Insulation, stuff));
                if (g <= gain) continue;
                gain = g;
                best = d;
                inStore = null;
            }
            return best;
        }

        // ---------------- The wish ----------------

        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            var map = HomeMap(pawn);
            if (map == null || pawn.apparel == null || pawn.DevelopmentalStage.Baby() || !SeasonUtil.HasSeasons(map)) yield break;
            float season = SeasonTemp(map, Heat);
            if (Heat ? season < HotSeason : season > ColdSeason) yield break;
            float comfy = pawn.GetStatValue(Comfy);
            if ((Heat ? season - comfy : comfy - season) < SeasonWishMath.AskGap) yield break;
            var best = BestPiece(pawn, map, out float gain, out _);
            float target = SeasonWishMath.ClothesTarget(comfy, season, gain, Heat);
            if (best == null || float.IsNaN(target)) yield break;
            float outside = map.mapTemperature?.OutdoorTemp ?? 15f;
            bool feelsIt = Heat ? outside > comfy : outside < comfy;
            float weight = (feelsIt ? 1.4f : 1f) * (Flavor.HumanAge(pawn) >= 60f ? 1.2f : 1f);
            string reason = (Heat ? "CC_Reason_SeasonCool" : "CC_Reason_SeasonWarm").Translate(pawn.Named("PAWN"),
                comfy.ToStringTemperature("F0").Named("COMFY"), season.ToStringTemperature("F0").Named("TEMP"), SeasonLabel(map).Named("SEASON")).Resolve();
            var c = Candidate(weight, reason);
            c.thingDef = best;
            c.targetValue = target;
            c.baseline = comfy;
            c.subjectKey = pawn.ThingID;
            c.subjectLabel = SeasonLabel(map);
            yield return c;
        }

        private float ComfyOf(Wish wish) => wish.Beneficiary?.GetStatValue(Comfy) ?? (Heat ? float.MinValue : float.MaxValue);

        public override bool IsFulfilled(Wish wish) => SeasonWishMath.Covered(ComfyOf(wish), wish.targetValue, Heat);

        /// <summary>The season it was for is over (or they're somewhere without seasons): let go without blame.</summary>
        public override bool StillValid(Wish wish)
        {
            var map = wish.Beneficiary?.MapHeld;
            if (map == null) return true;
            if (!SeasonUtil.HasSeasons(map)) return false;
            return !SeasonWishMath.SeasonPassed(ComfyOf(wish), SeasonTemp(map, Heat), Heat);
        }

        public override string ProgressText(Wish wish) =>
            "CC_Progress_SeasonClothes".Translate(ComfyOf(wish).ToStringTemperature("F0"), wish.targetValue.ToStringTemperature("F0")).Resolve();

        public override float Progress(Wish wish)
        {
            float span = Mathf.Abs(wish.targetValue - wish.baseline);
            if (span < 0.5f) return -1f;
            return Mathf.Clamp01(Mathf.Abs(ComfyOf(wish) - wish.baseline) / span);
        }

        /// <summary>The def's text, with the temperature in the player's own unit (the text says it in °C).</summary>
        public override string FulfilledWhen(Wish wish) => wish.Format((def.fulfilledWhen ?? "").Replace("{TARGET}°C", wish.targetValue.ToStringTemperature("F0")));

        /// <summary>Promised: if the colony has something in store that would do, they go and put it on.</summary>
        public override string OnPromised(Wish wish)
        {
            var p = wish.Beneficiary;
            var map = p?.Map;
            if (map == null || !p.Spawned || p.Drafted || p.Downed || p.InMentalState || map.dangerWatcher.DangerRating != StoryDanger.None) return null;
            try
            {
                BestPiece(p, map, out float gain, out var item);
                if (item == null || gain < SeasonWishMath.MinGain || !p.CanReserveAndReach(item, PathEndMode.ClosestTouch, Danger.Some)) return null;
                var job = JobMaker.MakeJob(JobDefOf.Wear, item);
                if (!p.jobs.TryTakeOrderedJob(job, JobTag.Misc)) return null;
                wish.subjectLabel = wish.subjectLabel ?? item.LabelNoCount;
                return "CC_Promise_SeasonClothes".Translate(p.Named("FOR"), item.LabelNoCount.Named("ITEM")).Resolve();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("season clothes promise", e);
                return null;
            }
        }
    }

    /// <summary>Clothes warm enough for the coming cold.</summary>
    public class WishWorker_SeasonWarm : WishWorker_SeasonClothes
    {
        protected override bool Heat => false;
    }

    /// <summary>Clothes cool enough for the hot season, on land where the summers are fierce.</summary>
    public class WishWorker_SeasonCool : WishWorker_SeasonClothes
    {
        protected override bool Heat => true;
    }

    // ===================== A sauna =====================

    /// <summary>
    /// A sauna: Dubs Bad Hygiene's (a room with its sauna heater), or one the colony made itself, a small closed room with
    /// no bed or workbench, a seat, and a heater or a fire that keeps it hot (28 °C, or as hot as a fire can make a room).
    /// Found once an hour per map.
    /// </summary>
    public static class Sauna
    {
        private static readonly Dictionary<int, (int hour, List<Room> rooms)> found = new Dictionary<int, (int, List<Room>)>();
        private static Game game;

        public static List<Room> Rooms(Map map)
        {
            var none = new List<Room>();
            if (map == null) return none;
            if (game != Current.Game)
            {
                game = Current.Game;
                found.Clear();
            }
            int hour = Find.TickManager.TicksGame / GenDate.TicksPerHour;
            if (found.TryGetValue(map.uniqueID, out var c) && c.hour == hour) return c.rooms;
            var list = new List<Room>();
            try
            {
                foreach (var room in RoomUtil.ColonyRooms(map))
                    if (IsSauna(room)) list.Add(room);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("sauna rooms", e);
            }
            found[map.uniqueID] = (hour, list);
            return list;
        }

        public static bool Exists(Map map) => Rooms(map).Count > 0;

        /// <summary>A fire: the game's fire overlay, or something burning fuel for heat and light.</summary>
        public static bool IsFire(ThingDef d) =>
            d?.comps != null && (d.comps.Any(c => c is CompProperties_FireOverlay)
                                 || (d.comps.Any(c => c is CompProperties_Refuelable) && d.comps.Any(c => c is CompProperties_HeatPusher h && h.heatPerSecond > 0f) && d.comps.Any(c => c is CompProperties_Glower)));

        /// <summary>The warmth a heat source can drive a room to: a heater as far as it's set, a fire only so far.</summary>
        public static float Ceiling(ThingDef d)
        {
            if (d == null) return 0f;
            if ((d.thingClass != null && typeof(Building_Heater).IsAssignableFrom(d.thingClass)) || d.HasComp(typeof(CompTempControl))) return 99f;
            var hp = d.GetCompProperties<CompProperties_HeatPusher>();
            if (hp == null) return 99f;
            return hp.heatPushMaxTemperature > 0f && hp.heatPushMaxTemperature < 900f ? hp.heatPushMaxTemperature : 99f;
        }

        /// <summary>Real heat: a sauna heater, a heater, or a fire big enough to warm a room (a campfire; not a torch).</summary>
        private static bool Heats(ThingDef d) => Hygiene.IsSaunaHeater(d) || Avail.IsHeater(d);

        public static bool IsSauna(Room room)
        {
            if (room == null || room.PsychologicallyOutdoors || room.UsesOutdoorTemperature || room.IsDoorway) return false;
            int cells = room.CellCount;
            if (cells < 2 || cells > SeasonWishMath.SaunaMaxCells) return false;
            bool seat = false, modded = false;
            float ceiling = -1f;
            var things = room.ContainedAndAdjacentThings;
            for (int i = 0; i < things.Count; i++)
            {
                if (!(things[i] is Building b) || b.Faction != Faction.OfPlayer || !room.ContainsCell(b.Position)) continue;
                var d = b.def;
                // A bedroom, a pen or a workshop is no sauna (a campfire's cooking aside: it's the fire).
                if (d.IsBed) return false;
                if (d.IsWorkTable && !IsFire(d)) return false;
                if (Hygiene.IsSaunaHeater(d)) modded = true;
                else if (Heats(d)) ceiling = Mathf.Max(ceiling, Ceiling(d));
                if (d.building != null && d.building.isSittable) seat = true;
            }
            if (modded) return true;
            return seat && ceiling > 0f && SeasonWishMath.SaunaHotEnough(room.Temperature, ceiling);
        }

        /// <summary>The heater, the sauna heater or the fire in it, to face while sitting there.</summary>
        public static Thing HeatIn(Room room)
        {
            if (room == null) return null;
            foreach (var t in room.ContainedAndAdjacentThings)
                if (t is Building b && room.ContainsCell(b.Position) && Heats(b.def)) return b;
            return null;
        }

        /// <summary>A place to sit in it: a free seat first, else a free cell of the room's floor.</summary>
        public static IntVec3 SpotIn(Room room, Pawn p)
        {
            var map = p?.Map;
            if (room == null || map == null) return IntVec3.Invalid;
            var dest = map.pawnDestinationReservationManager;
            IntVec3 floor = IntVec3.Invalid;
            foreach (var c in room.Cells)
            {
                if (!c.Standable(map) || !dest.CanReserve(c, p) || !p.CanReach(c, PathEndMode.OnCell, Danger.None)) continue;
                var there = c.GetFirstPawn(map);
                if (there != null && there != p) continue;
                var ed = c.GetEdifice(map);
                if (ed != null && ed.def.building != null && ed.def.building.isSittable) return c;
                if (!floor.IsValid && ed == null) floor = c;
            }
            return floor;
        }

        /// <summary>Cold out there: a freezing or near-freezing day, the winter, or colder than this one bears in what they wear.</summary>
        public static bool ColdOut(Pawn p)
        {
            var map = p?.Map;
            if (map == null || !SeasonUtil.HasSeasons(map)) return false;
            float outside = map.mapTemperature?.OutdoorTemp ?? 15f;
            var s = GenLocalDate.Season(map);
            return outside <= 5f || s == Season.Winter || s == Season.PermanentWinter || outside < p.GetStatValue(StatDefOf.ComfyTemperatureMin);
        }

        /// <summary>Cold land: its coldest twelfth averages freezing or less.</summary>
        public static bool ColdLand(Map map) => map != null && SeasonUtil.HasSeasons(map) && WishWorker_SeasonClothes.Averages(map).Min() <= 0f;

        private static ThoughtDef warmed;
        private static bool looked;

        /// <summary>
        /// They've sat down in the heat: warmed through (comfort and a little rest instead of mood), and, with someone else
        /// in there too, the two talk a while in the steam (the social web, through Together: only when they're really
        /// side by side).
        /// </summary>
        public static void Arrived(Pawn p)
        {
            if (p == null) return;
            if (!looked)
            {
                looked = true;
                warmed = DefDatabase<ThoughtDef>.GetNamedSilentFail("Council_Habit_SaunaWarmed");
            }
            if (warmed != null) CouncilGameComponent.GiveThought(p, warmed);
            var room = p.GetRoom();
            if (room == null || p.Map == null) return;
            foreach (var o in p.Map.mapPawns.FreeColonistsSpawned)
            {
                if (o == p || o.GetRoom() != room || !Together.Can(p, o)) continue;
                // together: both are sitting in the same small, hot room.
                var comp = CouncilGameComponent.Instance;
                if (ColonyCouncilMod.Settings.socialWeb && comp?.social != null)
                    comp.social.Bump(p, o, TieKind.Friends, 1f, "CC_TieNote_Sauna".Translate());
                string line = Flavor.FromPool("Council_Pool_SaunaTogether", p, null, null);
                if (!line.NullOrEmpty()) SocialLog.Add(p, o, line.Formatted(p.Named("PAWN"), o.Named("OTHER")).Resolve());
                break;
            }
        }
    }

    /// <summary>
    /// A sauna for the cold months: on cold land, someone chilled to the bone, getting on in years, or fond of company
    /// wants a small hot room to warm up in. With Dubs Bad Hygiene, a sauna heater and its room; without it, a sauna of
    /// the colony's own: a small closed room with a seat and a heater (a fire, before electricity) kept hot. Fulfilled
    /// when one stands; its people use it on cold days (the small habit, Sauna.Arrived).
    /// </summary>
    public class WishWorker_Sauna : WishWorker
    {
        private static ThingDef SaunaHeater(Map map) => Avail.BestBuildable(map, Hygiene.IsSaunaHeater, "DBHSaunaHeaterLog", "DBHSaunaHeaterElec");

        /// <summary>The heat for a sauna of the colony's own: an electric heater once it can be built, else a campfire.</summary>
        private static ThingDef OwnHeat(Map map) =>
            Avail.BestBuildable(map, d => d.thingClass != null && typeof(Building_Heater).IsAssignableFrom(d.thingClass), "Heater")
            ?? Avail.BestBuildable(map, d => Sauna.IsFire(d) && Avail.IsHeater(d) && d.defName.IndexOf("Campfire", StringComparison.OrdinalIgnoreCase) >= 0, "Campfire");

        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            var map = HomeMap(pawn);
            if (map == null || pawn.DevelopmentalStage.Juvenile() || !Sauna.ColdLand(map) || Sauna.Exists(map)) yield break;
            var season = GenLocalDate.Season(map);
            if (season != Season.Fall && season != Season.Winter && season != Season.PermanentWinter) yield break;
            bool cold = HasMemory(pawn, ThoughtDefOf.SleptInCold) || (map.mapTemperature?.OutdoorTemp ?? 15f) < pawn.GetStatValue(StatDefOf.ComfyTemperatureMin);
            bool old = Flavor.HumanAge(pawn) >= 55f;
            bool sociable = PeopleTags.Has(pawn, "sociable") || MoreHabits.Feeling(pawn, "Loneliness") != null;
            float weight = cold ? 1.3f : old ? 1f : sociable ? 0.8f : 0.4f;
            var heat = SaunaHeater(map) ?? OwnHeat(map);
            if (heat == null || Avail.BestBuildable(map, Avail.IsChair) == null && !Hygiene.IsSaunaHeater(heat)) yield break;
            var c = Candidate(weight, (cold ? "CC_Reason_SaunaCold" : "CC_Reason_Sauna").Translate(pawn.Named("PAWN")).Resolve());
            c.thingDef = heat;
            c.subjectKey = "colony";
            yield return c;
        }

        public override bool IsFulfilled(Wish wish) => Sauna.Exists(wish.Beneficiary?.MapHeld);

        public override bool StillObtainable(Wish wish)
        {
            if (!base.StillObtainable(wish)) return false;
            var map = wish.Beneficiary?.MapHeld;
            return map == null || Sauna.Exists(map) || SaunaHeater(map) != null || OwnHeat(map) != null;
        }

        public override RoomTemplateDef SuggestedTemplate(Wish wish) =>
            wish.thingDef != null && Hygiene.IsSaunaHeater(wish.thingDef) ? def.template : DefDatabase<RoomTemplateDef>.GetNamedSilentFail("Council_SweatRoom") ?? def.template;

        public override string FulfilledWhen(Wish wish) =>
            wish.thingDef != null && Hygiene.IsSaunaHeater(wish.thingDef) ? "CC_Fulfilled_SaunaModded".Translate(wish.thingDef.label.Named("THING")).Resolve()
                : "CC_Fulfilled_SaunaOwn".Translate((wish.thingDef?.label ?? "?").Named("THING"), SeasonWishMath.SaunaHeat.ToStringTemperature("F0").Named("HEAT")).Resolve();

        // Promises kept: the sauna stays (its heater, its seat).
        public override KeptCheck Holds(Wish wish) => IsFulfilled(wish) ? KeptCheck.Holds : KeptCheck.Gone;

        public override bool Concerns(Wish wish, BuildableDef d) => d is ThingDef td && (Hygiene.IsSaunaHeater(td) || Avail.IsHeater(td) || Sauna.IsFire(td));

        public override IEnumerable<Thing> KeptThings(Wish wish)
        {
            foreach (var room in Sauna.Rooms(wish.Beneficiary?.MapHeld))
            {
                var heat = Sauna.HeatIn(room);
                if (heat != null) yield return heat;
            }
        }

        public override string LostText(Wish wish) => "CC_Kept_Lost_Sauna".Translate(wish.Beneficiary.Named("FOR")).Resolve();
    }

    /// <summary>
    /// The sauna's small habit: on a cold day, someone free goes to sit in the colony's sauna a while, facing the heat,
    /// talking with whoever else is in there (Sauna.Arrived). Not for one whose faith frowns on recreation or wants
    /// discomfort.
    /// </summary>
    public class JoyGiver_HabitSauna : JoyGiver_Habit
    {
        private static readonly ulong NotForThem = BeliefMath.Mask(new[] { Belief.NoRecreation, Belief.Discomfort });

        protected override HabitKind Kind => HabitKind.Sauna;

        protected override bool Fits(Pawn p) => !Beliefs.ForbidsAny(p, NotForThem) && Sauna.ColdOut(p) && Sauna.Exists(p.Map);

        protected override Job Give(Pawn p)
        {
            var room = Sauna.Rooms(p.Map).Where(r => r.Cells.Any() && p.CanReach(r.Cells.First(), PathEndMode.OnCell, Danger.None))
                .OrderBy(r => r.Cells.First().DistanceToSquared(p.Position)).FirstOrDefault();
            if (room == null) return null;
            var cell = Sauna.SpotIn(room, p);
            if (!cell.IsValid) return null;
            var heat = Sauna.HeatIn(room);
            return Make(cell, heat != null ? (LocalTargetInfo)heat : LocalTargetInfo.Invalid, new IntRange(1500, 2500));
        }
    }
}
