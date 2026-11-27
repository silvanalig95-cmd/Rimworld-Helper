using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // The content round's fourth set of council questions (docs/content-round-4-council-world.md, R4C-1 to R4C-10; Defs/
    // DilemmaDefs/Rimfolk_Round4_Council.xml), in the flavour style of the council's questions: the stone by the walls, the
    // night owl, the seat while its holder is away, too hard on one, the visiting master, the cold cell, a rest for a slave, a
    // bed for the animal, the dry grass by the walls and words before supper. Each answer plays out on the map with what
    // exists; the few effects here are thin pieces over the game's own ways: plants marked for cutting, a hand-over for the
    // decree's days, an animal bed set in a room, a visitor kept on for the afternoon. No saved collection: the contexts are
    // those that exist, and one that keeps a decree's name (R4CContext). The pure rules are World/Variety/Round4CouncilWorldMath.cs.

    /// <summary>A council question of the fourth round: the second round's (a god, a third, the work it is about) and the decree it is about ({DECREE}).</summary>
    public class R4CContext : Round2Context
    {
        public string decree;

        public DecreeDef Decree => decree.NullOrEmpty() ? null : DefDatabase<DecreeDef>.GetNamedSilentFail(decree);

        protected override void AddArgs(List<NamedArgument> args)
        {
            base.AddArgs(args);
            args.Add((Decree?.label ?? "").Named("DECREE"));
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref decree, "decree");
        }
    }

    /// <summary>Shared look-ups of the fourth round's council questions.</summary>
    public static class R4CCouncil
    {
        public const string BlocksRecipe = "Make_StoneBlocksAny";
        public const string NightOwlTrait = "NightOwl";
        public const string NightOwlDay = "NightOwlDuringTheDay";

        /// <summary>The animal beds a room may get, the finest first (the game's own; a mod's beds are its own kinds of these).</summary>
        public static readonly string[] AnimalBeds = { "AnimalBed", "AnimalSleepingBox", "AnimalSleepingSpot" };

        private static int Now => Find.TickManager.TicksGame;

        // ---------------- The stone by the walls ----------------

        /// <summary>The stone chunks lying in the home area out of storage, the game's or a mod's, not forbidden.</summary>
        public static List<Thing> LooseChunks(Map map)
        {
            if (map == null) return new List<Thing>();
            var home = map.areaManager.Home;
            return map.listerThings.ThingsInGroup(ThingRequestGroup.Chunk)
                .Where(t => t.Spawned && t.def.thingCategories != null && t.def.thingCategories.Contains(ThingCategoryDefOf.StoneChunks) && home[t.Position]
                            && !t.IsInAnyStorage() && !t.IsForbidden(Faction.OfPlayer)).ToList();
        }

        // ---------------- The night owl ----------------

        public static bool NightOwl(Pawn p)
        {
            var t = DefDatabase<TraitDef>.GetNamedSilentFail(NightOwlTrait);
            return t != null && (p?.story?.traits?.HasTrait(t) ?? false);
        }

        /// <summary>One who shares a pawn's bedroom (another owner of a bed in it), or null.</summary>
        public static Pawn RoomMate(Pawn p, List<Pawn> among)
        {
            var room = RoomUtil.BedRoom(p);
            if (room == null || room.PsychologicallyOutdoors) return null;
            return room.ContainedBeds.SelectMany(b => b.OwnersForReading).FirstOrDefault(o => o != p && among.Contains(o));
        }

        /// <summary>How hard-working someone is by the game's own trait (its degree), 0 without it.</summary>
        public static int Industry(Pawn p)
        {
            var t = DefDatabase<TraitDef>.GetNamedSilentFail("Industriousness");
            return t == null ? 0 : p?.story?.traits?.DegreeOfTrait(t) ?? 0;
        }

        // ---------------- The seat while its holder is away ----------------

        /// <summary>A holder away from the home map: in a caravan, on a far errand or on another map.</summary>
        public static bool Away(Pawn h, Map home) => h != null && !h.Dead && h.MapHeld != home;

        // ---------------- Too hard on one ----------------

        /// <summary>The decree's days left, from its end.</summary>
        public static float DaysLeft(ActiveDecree a) => a == null ? 0f : (a.endTick - Now) / (float)GenDate.TicksPerDay;

        /// <summary>The work someone gives first place to of those with a skill (skilled or not), the work they'd hand over.</summary>
        public static WorkTypeDef HeavyWork(Pawn p)
        {
            var skilled = Round3Council.MainWork(p);
            if (skilled != null) return skilled;
            var ws = p?.workSettings;
            if (ws == null || !ws.EverWork) return null;
            return DefDatabase<WorkTypeDef>.AllDefsListForReading
                .Where(w => !w.relevantSkills.NullOrEmpty() && !p.WorkTypeIsDisabled(w) && ws.GetPriority(w) > 0)
                .OrderBy(w => ws.GetPriority(w)).ThenByDescending(w => Round3Council.Level(p, w)).FirstOrDefault();
        }

        // ---------------- The visiting master ----------------

        /// <summary>A visitor or a trader of a people at peace, grown and up, here on the map.</summary>
        public static bool Visiting(Pawn x)
        {
            var k = Strangers.KindOf(x);
            return (k == StrangerKind.Visitor || k == StrangerKind.Trader) && x.Faction != null && !x.Faction.HostileTo(Faction.OfPlayer) && x.skills != null && !x.DevelopmentalStage.Juvenile();
        }

        public static int LevelOf(Pawn p, SkillDef s) => p?.skills?.GetSkill(s) is SkillRecord r && !r.TotallyDisabled ? r.Level : -1;

        // ---------------- The cold cell ----------------

        /// <summary>A prisoner chilled in their cell: the game's own hypothermia, in a proper room colder than the rule.</summary>
        public static bool ColdInCell(Pawn x)
        {
            if (x == null || x.Dead || !x.IsPrisonerOfColony || HediffDefOf.Hypothermia == null || x.health?.hediffSet?.HasHediff(HediffDefOf.Hypothermia) != true) return false;
            var room = RoomUtil.BedRoom(x);
            return room != null && room.ProperRoom && !room.PsychologicallyOutdoors && room.Temperature < Round4CouncilWorldMath.ColdCellBelow;
        }

        /// <summary>The colony's warden: the best with people of those who do the warden's work.</summary>
        public static Pawn Warden(IEnumerable<Pawn> pawns, Pawn not = null) =>
            pawns.Where(p => p != not && !p.WorkTypeIsDisabled(WorkTypeDefOf.Warden) && (p.workSettings?.GetPriority(WorkTypeDefOf.Warden) ?? 0) > 0)
                .OrderByDescending(p => VarietyThree.Level(p, SkillDefOf.Social)).FirstOrDefault();

        // ---------------- A rest for a slave ----------------

        public static bool SlaveWorn(Pawn x)
        {
            if (!ModsConfig.IdeologyActive || x == null || x.Dead || !x.IsSlaveOfColony || !x.Spawned || x.Downed || x.InMentalState) return false;
            float joy = x.needs?.joy?.CurLevelPercentage ?? -1f;
            float mood = x.needs?.mood?.CurLevelPercentage ?? 1f;
            float held = x.needs?.TryGetNeed<Need_Suppression>()?.CurLevelPercentage ?? 0f;
            return Round4CouncilWorldMath.SlaveWorn(joy, mood, held);
        }

        // ---------------- A bed for the animal ----------------

        /// <summary>The animal bonded to someone, here on the map, small and not one kept in a pen.</summary>
        public static Pawn SmallBonded(Pawn a, Map map)
        {
            var rel = a?.relations?.DirectRelations;
            if (rel == null) return null;
            foreach (var r in rel)
            {
                var pet = r?.otherPawn;
                if (r?.def != PawnRelationDefOf.Bond || pet == null || pet.Dead || !pet.Spawned || pet.Map != map || pet.Faction != Faction.OfPlayer || !pet.RaceProps.Animal) continue;
                if (pet.BodySize > Round4CouncilWorldMath.PetBodyMax || AnimalPenUtility.NeedsToBeManagedByRope(pet)) continue;
                return pet;
            }
            return null;
        }

        /// <summary>A bed for an animal small enough for a corner of a room: the game's or any mod's (its own property, not a list).</summary>
        public static bool AnimalBedDef(ThingDef d) =>
            d != null && d.IsBed && d.building != null && !d.building.bed_humanlike && d.size.x * d.size.z <= 2 && d.category == ThingCategory.Building;

        /// <summary>
        /// The finest animal bed the colony can build here and that fits its age (as a thing made for an answer must,
        /// DilemmaEffect_Made.Pick), of every mod's and the game's (by its worth), else the game's own by name; or null.
        /// </summary>
        public static ThingDef BedToBuild(Map map)
        {
            bool Ok(ThingDef d) => d != null && Avail.CanBuild(d, map) && (d.techLevel == TechLevel.Undefined || d.techLevel <= Avail.EffectiveTech);
            var any = DefDatabase<ThingDef>.AllDefsListForReading.Where(d => AnimalBedDef(d) && Ok(d))
                .OrderByDescending(d => d.BaseMarketValue).ThenBy(d => d.defName).FirstOrDefault();
            return any ?? AnimalBeds.Select(n => DefDatabase<ThingDef>.GetNamedSilentFail(n)).FirstOrDefault(Ok);
        }

        /// <summary>A room with an animal's bed or sleeping spot in it already.</summary>
        public static bool HasAnimalBed(Room room) => room != null && room.ContainedBeds.Any(b => b.def.building != null && !b.def.building.bed_humanlike);

        // ---------------- The dry grass by the walls ----------------

        /// <summary>
        /// Dry wild growth at the edge of the colony's home ground (on it, and two steps out): grass and brush in the open, never a
        /// tree, a sown or sowable plant, wild food, a plant in a growing zone or one already marked.
        /// </summary>
        public static List<Plant> DryGrowth(Map map, int max)
        {
            var list = new List<Plant>();
            if (map == null) return list;
            try
            {
                var home = map.areaManager.Home;
                var seen = new HashSet<IntVec3>();
                foreach (var c in home.ActiveCells)
                {
                    bool edge = false;
                    for (int i = 0; i < 4 && !edge; i++)
                    {
                        var n = c + GenAdj.CardinalDirections[i];
                        if (!n.InBounds(map) || !home[n]) edge = true;
                    }
                    if (!edge) continue;
                    foreach (var x in GenRadial.RadialCellsAround(c, Round4CouncilWorldMath.FirebreakReach, true))
                    {
                        if (!x.InBounds(map) || !seen.Add(x) || x.Roofed(map) || x.Fogged(map) || map.zoneManager.ZoneAt(x) is Zone_Growing) continue;
                        var p = x.GetPlant(map);
                        if (p == null || p.def.plant == null || p.def.plant.IsTree || !p.def.plant.sowTags.NullOrEmpty() || !p.FlammableNow) continue;
                        if (p.def.plant.harvestedThingDef?.IsNutritionGivingIngestible == true) continue;
                        if (map.designationManager.DesignationOn(p) != null) continue;
                        list.Add(p);
                        if (list.Count >= max) return list;
                    }
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("round 4 council: the dry grass", e);
            }
            return list;
        }

        public static bool Raining(Map map) => map != null && (map.weatherManager.RainRate > 0.01f || map.weatherManager.SnowRate > 0.01f);

        // ---------------- Words before supper ----------------

        /// <summary>The colony eats at a table here (the game's own eating surface).</summary>
        public static bool HasTable(Map map) => map?.listerBuildings.allBuildingsColonist.Any(b => b.def.surfaceType == SurfaceType.Eat) ?? false;
    }

    // ===================================================================================================
    // R4C-1. The stone by the walls
    // ===================================================================================================

    /// <summary>
    /// Thirty stone chunks or more lie in the home area out of storage. A is the best builder, B the best grower; the
    /// question's recipe the blocks from any stone, its bench a stonecutter's (if there is one), {COUNT} the blocks bills.
    /// </summary>
    public class DilemmaWorker_R4CStone : DilemmaWorker
    {
        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!CanHappenNow(map)) return false;
            var grown = Round3Council.Grown(map);
            if (grown.Count < def.minColonists) return false;
            int chunks = R4CCouncil.LooseChunks(map).Count;
            if (!Round4CouncilWorldMath.StoneWorth(chunks)) return false;
            var a = VarietyThree.Best(grown.Where(p => def.roleA.Allows(p, null)), SkillDefOf.Construction, WorkTypeDefOf.Construction);
            if (a == null) return false;
            var b = VarietyThree.Best(grown, SkillDefOf.Plants, WorkTypeDefOf.Growing, a) ?? VarietyThree.Other(grown, a, SkillDefOf.Plants);
            if (b == null) return false;
            var recipe = DefDatabase<RecipeDef>.GetNamedSilentFail(R4CCouncil.BlocksRecipe);
            ctx = new DilemmaContextMore { def = def, map = map, a = a, b = b, recipe = recipe, place = Round3Council.Bench(map, recipe), count = Round4CouncilWorldMath.BlockBills(chunks) };
            return true;
        }
    }

    // ===================================================================================================
    // R4C-2. The night owl
    // ===================================================================================================

    /// <summary>
    /// A grown-up with the game's night owl trait has the game's own mood for being awake by day, and their mood is under
    /// half. B is the one who shares their room, else the most hard-working other grown-up.
    /// </summary>
    public class DilemmaWorker_R4CNightOwl : DilemmaWorker
    {
        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!CanHappenNow(map)) return false;
            var grown = Round3Council.Grown(map);
            if (grown.Count < def.minColonists) return false;
            var day = new List<string> { R4CCouncil.NightOwlDay };
            foreach (var a in grown.Where(p => def.roleA.Allows(p, null) && R4CCouncil.NightOwl(p)).InRandomOrder())
            {
                if (!Round4CouncilWorldMath.OwlLow(a.needs?.mood?.CurLevelPercentage ?? 1f) || !LifeChecks.HasThought(a, day, 1f)) continue;
                var b = R4CCouncil.RoomMate(a, grown) ?? grown.Where(p => p != a && !R4CCouncil.NightOwl(p)).OrderByDescending(R4CCouncil.Industry).ThenBy(_ => Rand.Value).FirstOrDefault();
                if (b == null) continue;
                ctx = new DilemmaContext { def = def, map = map, a = a, b = b };
                return true;
            }
            return false;
        }

        public override void AddTags(DilemmaContext ctx, HashSet<string> tags)
        {
            if (ctx?.a != null && ctx.b != null && R4CCouncil.RoomMate(ctx.a, new List<Pawn> { ctx.b }) == ctx.b) tags.Add("roommate");
        }
    }

    // ===================================================================================================
    // R4C-3. The seat while its holder is away
    // ===================================================================================================

    /// <summary>
    /// A seat's holder (A) is away from the home map (a caravan, a far errand, another map), with no deputy in and no election
    /// under way. B is the one at home most able at the seat's work who holds no seat. Tags: caravan or far.
    /// </summary>
    public class DilemmaWorker_R4CSeatAway : DilemmaWorker
    {
        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!SeatCalls.On || !CanHappenNow(map)) return false;
            var council = OfficeTracker.For(map);
            if (council == null || council.election != null) return false;
            var home = map.mapPawns.FreeColonistsSpawned.Where(p => !p.IsQuestLodger()).ToList();
            if (home.Count < def.minColonists) return false;
            foreach (var office in OfficeTracker.Offices.InRandomOrder())
                for (int seat = 0; seat < office.Seats; seat++)
                {
                    var h = council.HolderOf(office, seat);
                    if (!R4CCouncil.Away(h, map) || council.HasDeputy(h)) continue;
                    var b = home.Where(p => p != h && OfficeTracker.HeldBy(p) == null && office.CanHold(p) && !p.Downed && !p.InMentalState && !p.DevelopmentalStage.Juvenile())
                        .OrderByDescending(office.Competence).FirstOrDefault();
                    if (b == null) continue;
                    string tag = CaravanUtility.GetCaravan(h) != null ? "caravan" : "far";
                    ctx = new DilemmaContextLife { def = def, map = map, a = h, b = b, office = office, seat = seat, tag = tag };
                    return true;
                }
            return false;
        }

        public override void AddTags(DilemmaContext ctx, HashSet<string> tags)
        {
            if (ctx is DilemmaContextLife l && !l.tag.NullOrEmpty()) tags.Add(l.tag);
        }
    }

    // ===================================================================================================
    // R4C-4. Too hard on one
    // ===================================================================================================

    /// <summary>
    /// A decree of hard work is in force with a day or more left, and it falls hard on someone (A) who dislikes it: an elder,
    /// one ill, one troubled by an old injury, or one worn. B is the best at A's main work of the others who can do it.
    /// {DECREE} the decree; the work A would hand over the context's note. Tags: elder, ill, injured or worn.
    /// </summary>
    public class DilemmaWorker_R4CDecreeToll : DilemmaWorker
    {
        private static string Why(Pawn p)
        {
            if (Flavor.HumanAge(p) >= Round4CouncilWorldMath.ElderAge) return "elder";
            if (LifeChecks.HasIllness(p, null)) return "ill";
            if (LifeChecks.OldInjury(p)) return "injured";
            return "worn";
        }

        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!CanHappenNow(map)) return false;
            var tracker = DecreeTracker.For(map);
            if (tracker == null || tracker.active.Count == 0) return false;
            var grown = Round3Council.Grown(map);
            if (grown.Count < def.minColonists) return false;
            foreach (var d in tracker.active.Where(x => x?.def != null && Round4CouncilWorldMath.HardDecree(x.def.strainPerDay, R4CCouncil.DaysLeft(x))).InRandomOrder())
                foreach (var a in grown.Where(p => def.roleA.Allows(p, null)).InRandomOrder())
                {
                    if (d.def.ReactionOf(a) > DecreeReaction.Dislike) continue;
                    bool hard = Round4CouncilWorldMath.FallsHard(Flavor.HumanAge(a), LifeChecks.HasIllness(a, null), LifeChecks.OldInjury(a), Heart.StrainOf(a));
                    if (!hard) continue;
                    var work = R4CCouncil.HeavyWork(a);
                    if (work == null) continue;
                    var b = grown.Where(p => p != a && !p.WorkTypeIsDisabled(work) && (def.roleB == null || def.roleB.Allows(p, a))).OrderByDescending(p => Round3Council.Level(p, work)).FirstOrDefault();
                    if (b == null) continue;
                    ctx = new R4CContext { def = def, map = map, a = a, b = b, note = work.defName, decree = d.def.defName };
                    return true;
                }
            return false;
        }

        public override void AddTags(DilemmaContext ctx, HashSet<string> tags)
        {
            if (ctx?.a != null) tags.Add(Why(ctx.a));
        }
    }

    /// <summary>
    /// A's own work of the question ({WORK}, the context's note) handed to B while the decree lasts (its days left, rounded up)
    /// and put back after (AnswerShifts), as <see cref="DilemmaEffect_HandOver"/>. Nothing once the decree has ended.
    /// </summary>
    public class DilemmaEffect_R4CHandOver : DilemmaEffect_HandOver
    {
        private bool Set(DilemmaContext ctx)
        {
            var c = ctx as R4CContext;
            var d = c?.Decree;
            var active = d == null || ctx.map == null ? null : DecreeTracker.For(ctx.map)?.Get(d);
            if (active == null || c.note.NullOrEmpty()) return false;
            work = c.note;
            days = Round4CouncilWorldMath.HandOverDays(R4CCouncil.DaysLeft(active));
            return true;
        }

        public override string Unavailable(DilemmaContext ctx) =>
            ctx is R4CContext c && c.Decree != null && Set(ctx) ? null : "CC_R4C_Decree_Over".Translate(((ctx as R4CContext)?.Decree?.label ?? "").Named("DECREE")).Resolve();

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (Set(ctx)) base.Describe(ctx, sheet);
        }

        public override void Apply(DilemmaContext ctx)
        {
            if (Set(ctx)) base.Apply(ctx);
        }
    }

    // ===================================================================================================
    // R4C-5. The visiting master
    // ===================================================================================================

    /// <summary>
    /// A visitor or trader of a people at peace (X) is ten or better at a skill ({SKILL}) and five or more above the colony's
    /// best at it, and a grown-up with a passion for it (A) is at home. B is another grown-up, the warden first.
    /// </summary>
    public class DilemmaWorker_R4CVisitingMaster : DilemmaWorker
    {
        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!CanHappenNow(map)) return false;
            var grown = Round3Council.Grown(map);
            if (grown.Count < def.minColonists) return false;
            var visitors = map.mapPawns.AllPawnsSpawned.Where(R4CCouncil.Visiting).ToList();
            if (visitors.Count == 0) return false;
            foreach (var x in visitors.InRandomOrder())
                foreach (var s in DefDatabase<SkillDef>.AllDefsListForReading.InRandomOrder())
                {
                    int theirs = R4CCouncil.LevelOf(x, s);
                    int ours = grown.Max(p => R4CCouncil.LevelOf(p, s));
                    if (!Round4CouncilWorldMath.Master(theirs, ours)) continue;
                    var a = grown.Where(p => def.roleA.Allows(p, null) && p.skills?.GetSkill(s) is SkillRecord r && !r.TotallyDisabled && People.Passionate(r)).OrderByDescending(p => R4CCouncil.LevelOf(p, s)).FirstOrDefault();
                    if (a == null) continue;
                    var b = R4CCouncil.Warden(grown, a) ?? VarietyThree.Other(grown, a, SkillDefOf.Social);
                    if (b == null) continue;
                    ctx = new DilemmaContext { def = def, map = map, a = a, b = b, stranger = x, skill = s, faction = x.Faction };
                    return true;
                }
            return false;
        }
    }

    /// <summary>
    /// The visitor (X) stays on the afternoon for what the answer set going with them (a lesson, a supper), the game's own
    /// visit lengthened (VisitorServices.Stay). Nothing once they have gone.
    /// </summary>
    public class DilemmaEffect_R4CStay : DilemmaEffect
    {
        public float hours = Round4CouncilWorldMath.StayHours;

        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
        }

        public override void Apply(DilemmaContext ctx)
        {
            var x = ctx?.stranger;
            if (x == null || x.Dead || !x.Spawned || ctx.map == null || x.Map != ctx.map || Strangers.IsHospitalityGuest(x)) return;
            try
            {
                VisitorServices.Stay(x, Mathf.RoundToInt(hours * GenDate.TicksPerHour));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("round 4 council: the visitor stays on", e);
            }
        }
    }

    // ===================================================================================================
    // R4C-6. The cold cell
    // ===================================================================================================

    /// <summary>
    /// A prisoner of the colony (X) has the game's own hypothermia in a cell colder than the rule. A is the warden (the best
    /// with people of those who do the warden's work, else the best with people), B the one who thinks least of X.
    /// </summary>
    public class DilemmaWorker_R4CColdCell : DilemmaWorker
    {
        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!CanHappenNow(map)) return false;
            var grown = Round3Council.Grown(map);
            if (grown.Count < def.minColonists) return false;
            foreach (var x in map.mapPawns.PrisonersOfColonySpawned.Where(R4CCouncil.ColdInCell).InRandomOrder())
            {
                var a = R4CCouncil.Warden(grown.Where(p => def.roleA.Allows(p, null))) ?? VarietyThree.Best(grown.Where(p => def.roleA.Allows(p, null)), SkillDefOf.Social);
                if (a == null) continue;
                var b = grown.Where(p => p != a).OrderBy(p => Round3Council.Opinion(p, x)).FirstOrDefault();
                if (b == null) continue;
                ctx = new DilemmaContext { def = def, map = map, a = a, b = b, stranger = x, faction = x.Faction };
                return true;
            }
            return false;
        }
    }

    // ===================================================================================================
    // R4C-7. A rest for a slave
    // ===================================================================================================

    /// <summary>
    /// A slave of the colony (X, Ideology) whose recreation is nearly gone and whose mood is low, held down well enough, and a
    /// grown-up who thinks well of them (A). B keeps the slaves to their work: the warden other than A, else the one who thinks
    /// least of X.
    /// </summary>
    public class DilemmaWorker_R4CSlaveRest : DilemmaWorker
    {
        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!ModsConfig.IdeologyActive || !CanHappenNow(map)) return false;
            var grown = Round3Council.Grown(map).Where(p => !p.IsSlave).ToList();
            if (grown.Count < def.minColonists) return false;
            foreach (var x in map.mapPawns.SlavesOfColonySpawned.Where(R4CCouncil.SlaveWorn).InRandomOrder())
            {
                var a = grown.Where(p => def.roleA.Allows(p, null) && Round3Council.Opinion(p, x) >= Round4CouncilWorldMath.SlaveFondFrom).OrderByDescending(p => Round3Council.Opinion(p, x)).FirstOrDefault();
                if (a == null) continue;
                var b = R4CCouncil.Warden(grown, a) ?? grown.Where(p => p != a).OrderBy(p => Round3Council.Opinion(p, x)).FirstOrDefault();
                if (b == null) continue;
                ctx = new DilemmaContext { def = def, map = map, a = a, b = b, stranger = x, faction = x.Faction };
                return true;
            }
            return false;
        }
    }

    // ===================================================================================================
    // R4C-8. A bed for the animal
    // ===================================================================================================

    /// <summary>
    /// A grown-up with a bedroom of their own (A) is bonded to a small animal that isn't kept in a pen ({PET}), and the room
    /// has no animal bed. B is the colony's best with animals other than A; {THING} the finest animal bed the colony can build.
    /// </summary>
    public class DilemmaWorker_R4CPetBed : DilemmaWorker
    {
        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!CanHappenNow(map)) return false;
            var grown = Round3Council.Grown(map);
            if (grown.Count < def.minColonists) return false;
            var bed = R4CCouncil.BedToBuild(map);
            if (bed == null) return false;
            foreach (var a in grown.Where(p => def.roleA.Allows(p, null)).InRandomOrder())
            {
                var room = RoomUtil.PrivateRoom(a);
                if (room == null || room.PsychologicallyOutdoors || R4CCouncil.HasAnimalBed(room)) continue;
                var pet = R4CCouncil.SmallBonded(a, map);
                if (pet == null) continue;
                var b = VarietyThree.Best(grown, SkillDefOf.Animals, WorkTypeDefOf.Handling, a) ?? VarietyThree.Other(grown, a, SkillDefOf.Animals);
                if (b == null) continue;
                ctx = new DilemmaContext { def = def, map = map, a = a, b = b, pet = pet, thing = bed };
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// The question's animal bed ({THING}) set in a corner of A's own bedroom for their animal (<see cref="DilemmaEffect_Made"/>
    /// in the room). Can't be chosen once the room has one, or A has no room of their own.
    /// </summary>
    public class DilemmaEffect_R4CPetBed : DilemmaEffect_Made
    {
        public DilemmaEffect_R4CPetBed()
        {
            inRoom = true;
        }

        private void Set(DilemmaContext ctx)
        {
            if (ctx?.thing != null) things = ctx.thing.defName;
        }

        public override string Unavailable(DilemmaContext ctx)
        {
            var room = RoomUtil.PrivateRoom(ctx?.a);
            if (room == null) return "CC_R4C_PetBed_NoRoom".Translate((ctx?.a?.LabelShort ?? "").Named("PAWN")).Resolve();
            return R4CCouncil.HasAnimalBed(room) ? "CC_R4C_PetBed_Has".Translate().Resolve() : null;
        }

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            Set(ctx);
            base.Describe(ctx, sheet);
        }

        public override void Apply(DilemmaContext ctx)
        {
            Set(ctx);
            base.Apply(ctx);
        }
    }

    // ===================================================================================================
    // R4C-9. The dry grass by the walls
    // ===================================================================================================

    /// <summary>
    /// Summer, hot, no rain, and thirty or more dry wild plants at the edge of the home ground ({COUNT}). A is the best grower,
    /// B the best builder.
    /// </summary>
    public class DilemmaWorker_R4CDryGrass : DilemmaWorker
    {
        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!CanHappenNow(map)) return false;
            if (map.mapTemperature.OutdoorTemp < Round4CouncilWorldMath.DryHeatFrom || R4CCouncil.Raining(map)) return false;
            var grown = Round3Council.Grown(map);
            if (grown.Count < def.minColonists) return false;
            int n = R4CCouncil.DryGrowth(map, Round4CouncilWorldMath.FirebreakMark).Count;
            if (!Round4CouncilWorldMath.DryDanger(map.mapTemperature.OutdoorTemp, false, n)) return false;
            var a = VarietyThree.Best(grown.Where(p => def.roleA.Allows(p, null)), SkillDefOf.Plants, WorkTypeDefOf.Growing);
            if (a == null) return false;
            var b = VarietyThree.Best(grown, SkillDefOf.Construction, WorkTypeDefOf.Construction, a) ?? VarietyThree.Other(grown, a, SkillDefOf.Construction);
            if (b == null) return false;
            ctx = new DilemmaContext { def = def, map = map, a = a, b = b, count = n };
            return true;
        }
    }

    /// <summary>
    /// The firebreak: the dry wild growth at the edge of the colony's home ground marked for cutting (the game's own
    /// designation), for the work party the same answer sets going. Nothing to mark: the words alone.
    /// </summary>
    public class DilemmaEffect_R4CFirebreak : DilemmaEffect
    {
        public int max = Round4CouncilWorldMath.FirebreakMark;

        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (ctx?.map == null) return;
            int n = R4CCouncil.DryGrowth(ctx.map, max).Count;
            if (n > 0) sheet.Play("CC_R4C_Impact_Firebreak".Translate(n).Resolve(), "CC_R4C_Impact_FirebreakBrief".Translate().Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            var map = ctx?.map;
            if (map == null) return;
            try
            {
                var plants = R4CCouncil.DryGrowth(map, max);
                if (plants.Count == 0) return;
                foreach (var p in plants) map.designationManager.AddDesignation(new Designation(p, DesignationDefOf.CutPlant));
                Tell.Message(NewsKind.Colony, Importance.Notable, map, "CC_R4C_FirebreakMarked".Translate(plants.Count.Named("N")).Resolve(), new LookTargets(plants.Take(20).Cast<Thing>().ToArray()),
                    MessageTypeDefOf.NeutralEvent);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("round 4 council: the firebreak", e);
            }
        }
    }

    // ===================================================================================================
    // R4C-10. Words before supper
    // ===================================================================================================

    /// <summary>
    /// The gods' worship on, a devout believer of a god with a place at this home (A, god1 their god), one who holds to no god
    /// (a scoffer, B), and a table the colony eats at.
    /// </summary>
    public class DilemmaWorker_R4CGrace : DilemmaWorker_Gods
    {
        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!Worship.On || !CanHappenNow(map) || !R4CCouncil.HasTable(map)) return false;
            var grown = Round3Council.Grown(map);
            if (grown.Count < def.minColonists) return false;
            var b = grown.Where(p => Worship.Ours(p) && Worship.Scoffer(p) && (def.roleB == null || def.roleB.Allows(p, null))).InRandomOrder().FirstOrDefault();
            if (b == null) return false;
            foreach (var a in grown.Where(p => p != b && def.roleA.Allows(p, null) && Worship.Ours(p) && !Worship.Scoffer(p) && Worship.Devout(p)).InRandomOrder())
            {
                var g = Worship.GodOf(a);
                if (g == null || g.kind == GodKind.Dead || Shrines.Of(map, g).Count == 0) continue;
                ctx = new Round2Context { def = def, map = map, a = a, b = b, god1 = g.id };
                return true;
            }
            return false;
        }
    }
}
