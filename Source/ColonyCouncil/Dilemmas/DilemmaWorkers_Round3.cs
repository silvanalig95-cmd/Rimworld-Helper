using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // The content round's third set of council questions (docs/content-round-3-council.md; Defs/DilemmaDefs/
    // Rimfolk_Round3_Council.xml), in the flavour style of the council's questions: the light sleeper, a grave for an animal,
    // what the insects left, the picking day (a custom of the calendar), the hungry gap, the forgetting, the crying in the
    // night, after the tantrum, the scoffer's grave and the table without meat. A friend among the visitors and a verse of
    // our own need no worker of their own (DilemmaWorker_Stranger, DilemmaWorker_WorldSeason). Each answer plays out on the
    // map with what exists; the few effects here are thin pieces over the game's own ways: beds traded, an empty grave
    // given, a bill at a bench, a supper for those who need it most first, a colonist's own work handed over for a season.
    // No saved collection: the contexts are those that exist (DilemmaContext, DilemmaContextMore, Round2Context).

    /// <summary>Shared look-ups of the third round's council questions.</summary>
    public static class Round3Council
    {
        public const string TantrumDeed = "Rimfolk_Deed_Tantrum";
        public const string VegRecipe = "Rimfolk_CookMealSimple_Veg";

        private static int Now => Find.TickManager.TicksGame;

        public static ThoughtDef ThoughtNamed(string name) => DefDatabase<ThoughtDef>.GetNamedSilentFail(name);

        public static List<ThoughtDef> Thoughts(params string[] names) => names.Select(ThoughtNamed).Where(t => t != null).ToList();

        /// <summary>The colony's grown-ups at home, up and in their right mind.</summary>
        public static List<Pawn> Grown(Map map) =>
            map.mapPawns.FreeColonistsSpawned.Where(p => !p.IsQuestLodger() && p.RaceProps.Humanlike && !p.DevelopmentalStage.Juvenile() && !p.Downed && !p.InMentalState).ToList();

        public static int Opinion(Pawn of, Pawn about) => of?.relations == null || about == null || of == about ? 0 : of.relations.OpinionOf(about);

        /// <summary>The newest memory of one of these kinds someone holds from the last <paramref name="days"/>, or null.</summary>
        public static Thought_Memory Memory(Pawn p, List<ThoughtDef> defs, float days)
        {
            var mems = p?.needs?.mood?.thoughts?.memories?.Memories;
            if (mems == null || defs.NullOrEmpty()) return null;
            Thought_Memory best = null;
            foreach (var m in mems)
                if (m != null && defs.Contains(m.def) && m.age <= days * GenDate.TicksPerDay && (best == null || m.age < best.age))
                    best = m;
            return best;
        }

        // ---------------- The light sleeper ----------------

        /// <summary>A single bed someone sleeps alone in, in a room shared with another sleeper (a barracks, a long hut).</summary>
        public static bool SharedSingle(Building_Bed bed)
        {
            if (bed == null || !bed.Spawned || bed.Medical || bed.ForPrisoners || bed.SleepingSlotsCount != 1 || bed.OwnersForReading.Count != 1) return false;
            var room = bed.GetRoom();
            return room != null && !room.PsychologicallyOutdoors && room.ContainedBeds.Count(b => b.OwnersForReading.Count > 0) >= 2;
        }

        /// <summary>How far a bed stands from the nearest door of its room, or -1 when the room has none.</summary>
        public static float FromDoor(Building_Bed bed)
        {
            var room = bed?.GetRoom();
            if (room == null) return -1f;
            float best = -1f;
            foreach (var t in room.ContainedAndAdjacentThings)
            {
                if (!(t is Building_Door door)) continue;
                float d = door.Position.DistanceTo(bed.Position);
                if (best < 0f || d < best) best = d;
            }
            return best;
        }

        // ---------------- Food ----------------

        /// <summary>Meat from an insect's body, the game's or a mod's.</summary>
        public static bool InsectMeat(ThingDef d) => d != null && d.IsMeat && d.ingestible?.sourceDef?.race?.FleshType == FleshTypeDefOf.Insectoid;

        private static bool Fresh(Thing t) => (t.TryGetComp<CompRottable>()?.Stage ?? RotStage.Fresh) == RotStage.Fresh;

        /// <summary>The stacks of a food kind lying in the colony's stores, fresh and not forbidden.</summary>
        public static List<Thing> Stored(Map map, Func<ThingDef, bool> kind) =>
            map.listerThings.ThingsInGroup(ThingRequestGroup.FoodSourceNotPlantOrTree)
                .Where(t => t.Spawned && kind(t.def) && t.IsInAnyStorage() && !t.IsForbidden(Faction.OfPlayer) && Fresh(t)).ToList();

        /// <summary>The kind of insect meat the stores hold most of, and how much food is in it.</summary>
        public static ThingDef MostInsectMeat(Map map, out float nutrition)
        {
            nutrition = 0f;
            var best = Stored(map, InsectMeat).GroupBy(t => t.def).OrderByDescending(g => g.Sum(t => t.stackCount * t.GetStatValue(StatDefOf.Nutrition))).FirstOrDefault();
            if (best == null) return null;
            nutrition = best.Sum(t => t.stackCount * t.GetStatValue(StatDefOf.Nutrition));
            return best.Key;
        }

        /// <summary>A bench of the colony's that can take a bill of this recipe now (the question's own first, when it is one).</summary>
        public static Building_WorkTable Bench(Map map, RecipeDef recipe, Thing prefer = null)
        {
            if (map == null || recipe == null || !recipe.AvailableNow) return null;
            if (prefer is Building_WorkTable p && p.Spawned && p.Map == map && p.def.AllRecipes != null && p.def.AllRecipes.Contains(recipe) && p.CurrentlyUsableForBills()) return p;
            return map.listerBuildings.allBuildingsColonist.OfType<Building_WorkTable>()
                .FirstOrDefault(b => b.Spawned && b.def.AllRecipes != null && b.def.AllRecipes.Contains(recipe) && b.CurrentlyUsableForBills());
        }

        /// <summary>A recipe that makes kibble and takes this meat (the game's, else a mod's), available now.</summary>
        public static RecipeDef Kibble(ThingDef meat) =>
            DefDatabase<RecipeDef>.AllDefsListForReading
                .Where(r => r.ProducedThingDef == ThingDefOf.Kibble && r.AvailableNow && r.fixedIngredientFilter != null && (meat == null || r.fixedIngredientFilter.Allows(meat)))
                .OrderByDescending(r => r.defName == "Make_Kibble").FirstOrDefault();

        /// <summary>The colony has an animal that eats kibble.</summary>
        public static bool KibbleEaters(Map map) => map?.mapPawns.SpawnedColonyAnimals.Any(a => !a.Dead && a.RaceProps.Animal && a.RaceProps.Eats(FoodTypeFlags.Kibble)) ?? false;

        // ---------------- The forgetting ----------------

        /// <summary>The skilled work someone gives first place to, of those they are good at: what the colony would hand over.</summary>
        public static WorkTypeDef MainWork(Pawn p)
        {
            var ws = p?.workSettings;
            if (ws == null || !ws.EverWork) return null;
            return DefDatabase<WorkTypeDef>.AllDefsListForReading
                .Where(w => !w.relevantSkills.NullOrEmpty() && !p.WorkTypeIsDisabled(w) && ws.GetPriority(w) > 0 && Level(p, w) >= Round3CouncilMath.SkilledFrom)
                .OrderBy(w => ws.GetPriority(w)).ThenByDescending(w => Level(p, w)).FirstOrDefault();
        }

        public static int Level(Pawn p, WorkTypeDef w) => w?.relevantSkills == null || w.relevantSkills.Count == 0 ? 0 : w.relevantSkills.Max(s => p?.skills?.GetSkill(s)?.Level ?? 0);

        public static bool Forgetting(Pawn p)
        {
            var set = p?.health?.hediffSet;
            if (set == null) return false;
            foreach (var name in new[] { "Dementia", "Alzheimers" })
            {
                var h = DefDatabase<HediffDef>.GetNamedSilentFail(name);
                if (h != null && set.HasHediff(h)) return true;
            }
            return false;
        }

        // ---------------- The hungry gap ----------------

        /// <summary>Someone who needs food most and eats first: a child, one of the colony's elders, someone ill.</summary>
        public static bool EatsFirst(Pawn p) =>
            p != null && p.RaceProps.Humanlike && !p.DevelopmentalStage.Baby()
            && Round3CouncilMath.EatsFirst(p.DevelopmentalStage.Child(), Flavor.HumanAge(p), LifeChecks.HasIllness(p, null));

        // ---------------- After the tantrum ----------------

        /// <summary>The tantrum someone had lately, as the colony's deeds keep it, or null.</summary>
        public static Deed Tantrum(Pawn p)
        {
            var tr = Reputations.On ? Reputations.Tracker : null;
            if (tr == null || p == null) return null;
            int now = Now;
            return tr.By(p.thingIDNumber).LastOrDefault(d => d?.def?.defName == TantrumDeed && Round3CouncilMath.Lately(now, d.tick, Round3CouncilMath.TantrumDays));
        }
    }

    // ===================================================================================================
    // The light sleeper
    // ===================================================================================================

    /// <summary>
    /// Someone (A) woke with the game's own "disturbed sleep" in the last day, in a single bed in a shared room nearer its
    /// door than another's: B, who sleeps alone at least two steps farther from it (not A's partner).
    /// </summary>
    public class DilemmaWorker_R3LightSleeper : DilemmaWorker
    {
        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!CanHappenNow(map)) return false;
            var grown = Round3Council.Grown(map);
            var woke = Round3Council.Thoughts("SleepDisturbed");
            if (grown.Count < def.minColonists || woke.Count == 0) return false;
            foreach (var a in grown.Where(p => def.roleA.Allows(p, null)).InRandomOrder())
            {
                if (Round3Council.Memory(a, woke, Round3CouncilMath.DisturbedDays) == null) continue;
                var bed = a.ownership?.OwnedBed;
                if (!Round3Council.SharedSingle(bed)) continue;
                float mine = Round3Council.FromDoor(bed);
                var partner = LovePartnerRelationUtility.ExistingLovePartner(a);
                Pawn b = null;
                float far = mine;
                foreach (var other in bed.GetRoom().ContainedBeds)
                {
                    if (other == bed || !Round3Council.SharedSingle(other)) continue;
                    var o = other.OwnersForReading[0];
                    if (o == a || o == partner || !grown.Contains(o)) continue;
                    float d = Round3Council.FromDoor(other);
                    if (Round3CouncilMath.Quieter(mine, d) && d > far)
                    {
                        b = o;
                        far = d;
                    }
                }
                if (b == null) continue;
                ctx = new DilemmaContext { def = def, map = map, a = a, b = b };
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// A and B trade beds, the game's own ownership: each sleeps in the other's from tonight. Only while each still sleeps
    /// alone in the bed the question was about; anything else, nothing changes.
    /// </summary>
    public class Round3CouncilSwapBeds : DilemmaEffect
    {
        public override bool ChanceEach => false;

        private static bool Fits(Pawn a, Pawn b, out Building_Bed bedA, out Building_Bed bedB)
        {
            bedA = a?.ownership?.OwnedBed;
            bedB = b?.ownership?.OwnedBed;
            return a != null && b != null && a != b && !a.Dead && !b.Dead && bedA != null && bedB != null && bedA != bedB && bedA.Spawned && bedB.Spawned
                   && bedA.Map == bedB.Map && !bedA.Medical && !bedB.Medical && bedA.OwnersForReading.Count == 1 && bedB.OwnersForReading.Count == 1;
        }

        public override string Unavailable(DilemmaContext ctx) => Fits(ctx?.a, ctx?.b, out _, out _) ? null : "CC_R3C_Beds_Gone".Translate().Resolve();

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (!Fits(ctx?.a, ctx?.b, out _, out _)) return;
            sheet.Play("CC_R3C_Impact_Beds".Translate(ctx.a.LabelShort, ctx.b.LabelShort).Resolve(), "CC_R3C_Impact_BedsBrief".Translate().Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            var a = ctx?.a;
            var b = ctx?.b;
            if (!Fits(a, b, out var bedA, out var bedB)) return;
            try
            {
                var assignA = bedA.CompAssignableToPawn;
                var assignB = bedB.CompAssignableToPawn;
                if (assignA == null || assignB == null || !assignB.CanAssignTo(a).Accepted || !assignA.CanAssignTo(b).Accepted) return;
                a.ownership.UnclaimBed();
                b.ownership.UnclaimBed();
                if (!(a.ownership.ClaimBedIfNonMedical(bedB) && b.ownership.ClaimBedIfNonMedical(bedA)))
                {
                    // Put back as it was.
                    a.ownership.UnclaimBed();
                    b.ownership.UnclaimBed();
                    a.ownership.ClaimBedIfNonMedical(bedA);
                    b.ownership.ClaimBedIfNonMedical(bedB);
                    return;
                }
                // apart: the council's ruling on whose bed is whose, the game's own ownership; they sleep in them from tonight.
                Tell.Message(NewsKind.Colony, Importance.Notable, ctx.map, "CC_R3C_BedsTraded".Translate(a.Named("PAWN"), b.Named("OTHER")).Resolve(), new LookTargets(new Thing[] { bedA, bedB }),
                    MessageTypeDefOf.NeutralEvent);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("round 3: beds traded", e);
            }
        }
    }

    // ===================================================================================================
    // A grave for an animal
    // ===================================================================================================

    /// <summary>
    /// A colonist's (A's) bonded animal died in the last two days (the game's own memory of it) and its body lies on the map,
    /// fresh and not yet laid anywhere. B is the colony's eldest other grown-up. {PET} and {DEAD} the animal; the place its
    /// body.
    /// </summary>
    public class DilemmaWorker_R3AnimalGrave : DilemmaWorker
    {
        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!CanHappenNow(map)) return false;
            var grown = Round3Council.Grown(map);
            var died = Round3Council.Thoughts("BondedAnimalDied");
            if (grown.Count < def.minColonists || died.Count == 0) return false;
            foreach (var a in grown.Where(p => def.roleA.Allows(p, null)).InRandomOrder())
            {
                var animal = Round3Council.Memory(a, died, Round3CouncilMath.AnimalDays)?.otherPawn;
                if (animal == null || !animal.Dead || !animal.RaceProps.Animal) continue;
                var body = animal.Corpse;
                if (body == null || !body.Spawned || body.Map != map || body.IsDessicated() || (body.TryGetComp<CompRottable>()?.Stage ?? RotStage.Fresh) != RotStage.Fresh) continue;
                var b = grown.Where(p => p != a && (def.roleB == null || def.roleB.Allows(p, a))).OrderByDescending(Flavor.HumanAge).FirstOrDefault();
                if (b == null) continue;
                ctx = new Round2Context { def = def, map = map, a = a, b = b, pet = animal, dead = animal, place = body };
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// One of the colony's empty graves given to the dead animal (a grave first, a sarcophagus if that is all there is): the
    /// game's own assigning, and its haulers lay the body there as they do the colony's own. Can't be chosen without an empty
    /// grave, or once the body is gone.
    /// </summary>
    public class Round3CouncilPetGrave : DilemmaEffect
    {
        public override bool ChanceEach => false;

        private static Corpse Body(DilemmaContext ctx) => (ctx as Round2Context)?.dead?.Corpse is Corpse c && c.Spawned && c.Map == ctx.map ? c : null;

        public static Building_Grave Grave(Map map) => Round2Council.EmptyGrave(map, false) ?? Round2Council.EmptyGrave(map, true);

        public override string Unavailable(DilemmaContext ctx)
        {
            if (ctx?.map == null) return null;
            if (Body(ctx) == null) return "CC_R3C_Grave_BodyGone".Translate(((ctx as Round2Context)?.dead?.LabelShort ?? "").Named("DEAD")).Resolve();
            return Grave(ctx.map) == null ? "CC_R3C_Grave_None".Translate().Resolve() : null;
        }

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var body = Body(ctx);
            if (body == null || Grave(ctx.map) == null) return;
            sheet.Play("CC_R3C_Impact_Grave".Translate(body.InnerPawn.LabelShort).Resolve(), "CC_R3C_Impact_GraveBrief".Translate().Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            var body = Body(ctx);
            var grave = ctx?.map == null ? null : Grave(ctx.map);
            if (body == null || grave == null) return;
            try
            {
                if (!(grave.TryGetComp<CompAssignableToPawn>() is CompAssignableToPawn assign)) return;
                assign.TryAssignPawn(body.InnerPawn);
                Tell.Message(NewsKind.Colony, Importance.Notable, ctx.map, "CC_R3C_GraveGiven".Translate(body.InnerPawn.LabelShort.Named("DEAD"), grave.LabelShort.Named("PLACE")).Resolve(),
                    new LookTargets(new Thing[] { grave, body }), MessageTypeDefOf.NeutralEvent);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("round 3: a grave for an animal", e);
            }
        }
    }

    // ===================================================================================================
    // What the insects left
    // ===================================================================================================

    /// <summary>
    /// The stores hold insect meat ({THING}) enough for eight simple meals or more ({COUNT}), and a fire or stove of the
    /// colony's cooks simple meals from it. A is the best cook, B the best hunter (else another grown-up).
    /// </summary>
    public class DilemmaWorker_R3HiveMeat : DilemmaWorker
    {
        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!CanHappenNow(map)) return false;
            var grown = Round3Council.Grown(map);
            if (grown.Count < def.minColonists) return false;
            var kind = Round3Council.MostInsectMeat(map, out float nutrition);
            if (kind == null) return false;
            int meals = VarietyThreeMath.Meals(nutrition, Spoiling.PerMeal(Spoiling.Meal));
            if (!Round3CouncilMath.InsectWorth(meals)) return false;
            var stove = Spoiling.Stove(map, kind);
            if (stove == null) return false;
            var cook = VarietyThree.Best(grown.Where(p => def.roleA.Allows(p, null)), SkillDefOf.Cooking, VarietyThree.Work("Cooking"));
            if (cook == null) return false;
            var b = VarietyThree.Best(grown, SkillDefOf.Shooting, WorkTypeDefOf.Hunting, cook) ?? VarietyThree.Other(grown, cook, SkillDefOf.Melee);
            if (b == null) return false;
            ctx = new DilemmaContextMore { def = def, map = map, a = cook, b = b, thing = kind, place = stove, recipe = Spoiling.Meal, count = meals };
            return true;
        }
    }

    /// <summary>
    /// A bill at a bench of the colony's, first on its stack, as the cooking bill for food that won't keep is
    /// (<see cref="DilemmaEffect_CookIt"/>): <see cref="recipe"/> by name, "@kibble" for a recipe that makes kibble from the
    /// question's meat, or the question's own recipe. With <see cref="ingredients"/> "plants" it takes the question's thing
    /// and plant food only (kibble of the insect meat), and is made as often as the stored thing allows; else from the
    /// recipe's own ingredients, {COUNT} times. Can't be chosen with no bench for it, no animal to eat kibble, or the thing gone.
    /// </summary>
    public class Round3CouncilBill : DilemmaEffect
    {
        public string recipe;
        public string ingredients = "default";

        public override bool ChanceEach => false;

        private bool Kibble => recipe == "@kibble";

        private RecipeDef Recipe(DilemmaContext ctx)
        {
            if (Kibble) return Round3Council.Kibble(ctx?.thing);
            if (!recipe.NullOrEmpty()) return DefDatabase<RecipeDef>.GetNamedSilentFail(recipe);
            return (ctx as DilemmaContextMore)?.recipe;
        }

        private Building_WorkTable Bench(DilemmaContext ctx, RecipeDef r) => Round3Council.Bench(ctx?.map, r, (ctx as DilemmaContextMore)?.place);

        private float StoredNutrition(DilemmaContext ctx) =>
            ctx?.thing == null ? 0f : Round3Council.Stored(ctx.map, d => d == ctx.thing).Sum(t => t.stackCount * t.GetStatValue(StatDefOf.Nutrition));

        private int Repeat(DilemmaContext ctx, RecipeDef r)
        {
            if (ingredients == "plants") return Round3CouncilMath.Bills(StoredNutrition(ctx), r?.ingredients?.FirstOrDefault()?.GetBaseCount() ?? 0f);
            return Math.Max(1, ctx?.count ?? 1);
        }

        public override string Unavailable(DilemmaContext ctx)
        {
            if (ctx?.map == null) return null;
            var r = Recipe(ctx);
            if (r == null || Bench(ctx, r) == null) return "CC_R3C_Bill_NoBench".Translate().Resolve();
            if (Kibble && !Round3Council.KibbleEaters(ctx.map)) return "CC_R3C_Bill_NoAnimals".Translate().Resolve();
            if (ingredients == "plants" && StoredNutrition(ctx) <= 0f) return "CC_R3C_Bill_Gone".Translate((ctx.thing?.label ?? "").Named("FOOD")).Resolve();
            return null;
        }

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var r = Recipe(ctx);
            var bench = Bench(ctx, r);
            if (r?.ProducedThingDef == null || bench == null) return;
            sheet.Play("CC_R3C_Impact_Bill".Translate(bench.LabelNoCount, Repeat(ctx, r), r.ProducedThingDef.label).Resolve(), "CC_R3C_Impact_BillBrief".Translate(r.ProducedThingDef.label).Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            var r = Recipe(ctx);
            var bench = Bench(ctx, r);
            if (r == null || bench == null) return;
            try
            {
                if (!(r.MakeNewBill() is Bill_Production bill)) return;
                if (ingredients == "plants" && ctx.thing != null && r.fixedIngredientFilter != null)
                {
                    // The question's meat for the meat, and plant food (or hay) for the rest: no other meat goes in.
                    bill.ingredientFilter.SetDisallowAll();
                    foreach (var d in r.fixedIngredientFilter.AllowedThingDefs)
                        if (d == ctx.thing || d == ThingDefOf.Hay || d.IsWithinCategory(ThingCategoryDefOf.PlantFoodRaw))
                            bill.ingredientFilter.SetAllow(d, true);
                }
                bill.repeatMode = BillRepeatModeDefOf.RepeatCount;
                bill.repeatCount = Repeat(ctx, r);
                VarietyThree.First(bench, bill);
                Tell.Message(NewsKind.Colony, Importance.Notable, ctx.map,
                    "CC_R3C_BillSet".Translate(bill.repeatCount.Named("N"), (r.ProducedThingDef?.label ?? r.label).Named("WHAT"), bench.LabelNoCount.Named("BENCH")).Resolve(), new LookTargets(bench),
                    MessageTypeDefOf.NeutralEvent);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("round 3: a bill at the bench", e);
            }
        }
    }

    // ===================================================================================================
    // The picking day
    // ===================================================================================================

    /// <summary>A custom of the calendar (the picking day): asked only while wild food is ripe within reach, as the foraging days read it.</summary>
    public class DilemmaWorker_R3PickingDay : DilemmaWorker_Custom
    {
        public override bool CanHappenNow(Map map) =>
            base.CanHappenNow(map) && YearFour.Forage(map, Round3CouncilMath.PickingFrom).Count >= Round3CouncilMath.PickingFrom;
    }

    // ===================================================================================================
    // The hungry gap
    // ===================================================================================================

    /// <summary>
    /// Fewer than four days of food in the stores ({COUNT}, the advisor's own count), cooked meals among them, and someone
    /// at home who needs food most (a child, an elder, the ill). A is the best cook, B the best at the hard outdoor work who
    /// is none of those.
    /// </summary>
    public class DilemmaWorker_R3HungryGap : DilemmaWorker
    {
        private static int Hard(Pawn p) => Mathf.Max(VarietyThree.Level(p, SkillDefOf.Mining), Mathf.Max(VarietyThree.Level(p, SkillDefOf.Construction), VarietyThree.Level(p, SkillDefOf.Plants)));

        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!CanHappenNow(map)) return false;
            var grown = Round3Council.Grown(map);
            if (grown.Count < def.minColonists) return false;
            float days = AdvisorWorker_Food.DaysOfFood(map);
            if (!Round3CouncilMath.Hungry(days) || PlayRounds.MealIn(map, null) == null) return false;
            if (!map.mapPawns.FreeColonistsSpawned.Any(Round3Council.EatsFirst)) return false;
            var cook = VarietyThree.Best(grown.Where(p => def.roleA.Allows(p, null)), SkillDefOf.Cooking, VarietyThree.Work("Cooking"));
            if (cook == null) return false;
            var b = grown.Where(p => p != cook && !Round3Council.EatsFirst(p)).OrderByDescending(Hard).FirstOrDefault();
            if (b == null) return false;
            ctx = new DilemmaContext { def = def, map = map, a = cook, b = b, count = Mathf.Max(1, Mathf.FloorToInt(days)) };
            return true;
        }
    }

    /// <summary>
    /// A supper for those who need it most, first (the feast engine: the meals from the stores eaten at the table): only the
    /// colony's children, its elders and the ill are asked to it, with the cook (A) bringing it.
    /// </summary>
    public class Round3CouncilFeastFirst : DilemmaEffect_Feast
    {
        public Round3CouncilFeastFirst()
        {
            duration = new FloatRange(1f, 1.5f);
            hours = new IntRange(17, 20);
        }

        private List<Pawn> First(DilemmaContext ctx) => Everyone(ctx).Where(Round3Council.EatsFirst).ToList();

        protected override void DescribePlay(DilemmaContext ctx, ImpactSheet sheet, string label)
        {
            var who = First(ctx);
            if (who.Count == 0) return;
            sheet.Play("CC_R3C_Impact_First".Translate(label, DilemmaImpact.When(hours), WhoOf(ctx, who)).Resolve(), label);
            Costs(ctx, sheet, who);
        }

        protected override bool Fill(PlayOut p, DilemmaContext ctx)
        {
            if (!base.Fill(p, ctx)) return false;
            var lead = p.organizer;
            p.invited = p.invited.Where(x => x == lead || Round3Council.EatsFirst(x)).ToList();
            return p.invited.Any(x => x != lead);
        }
    }

    // ===================================================================================================
    // The forgetting
    // ===================================================================================================

    /// <summary>
    /// A grown-up of the colony (A) has the game's dementia or Alzheimer's and still does a skilled work ({WORK}). B is the
    /// one closest to A who can do that work, else the best at it.
    /// </summary>
    public class DilemmaWorker_R3Forgetting : DilemmaWorker
    {
        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!CanHappenNow(map)) return false;
            var grown = Round3Council.Grown(map);
            if (grown.Count < def.minColonists) return false;
            foreach (var a in grown.Where(p => def.roleA.Allows(p, null) && Round3Council.Forgetting(p)).InRandomOrder())
            {
                var work = Round3Council.MainWork(a);
                if (work == null) continue;
                var can = grown.Where(p => p != a && !p.WorkTypeIsDisabled(work)).ToList();
                var b = can.Where(p => Round3Council.Opinion(p, a) >= Round3CouncilMath.CloseFrom).OrderByDescending(p => Round3Council.Opinion(p, a)).FirstOrDefault()
                        ?? can.OrderByDescending(p => Round3Council.Level(p, work)).FirstOrDefault();
                if (b == null) continue;
                ctx = new Round2Context { def = def, map = map, a = a, b = b, note = work.defName };
                return true;
            }
            return false;
        }
    }

    /// <summary>A's own work of the question ({WORK}, the context's note) handed to B for a while and put back after (AnswerShifts), as <see cref="DilemmaEffect_HandOver"/>.</summary>
    public class Round3CouncilHandOver : DilemmaEffect_HandOver
    {
        private void Set(DilemmaContext ctx)
        {
            string n = (ctx as Round2Context)?.note;
            if (!n.NullOrEmpty()) work = n;
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
    // The crying in the night
    // ===================================================================================================

    /// <summary>
    /// A baby of the colony (Biotech) has kept someone who sees to the children (A) up: the game's own crying-baby mood in
    /// the last day, and A's rest under half. B is another grown-up who can see to children, the fondest of A. {THIRD} the baby.
    /// </summary>
    public class DilemmaWorker_R3CryingNight : DilemmaWorker
    {
        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!ModsConfig.BiotechActive || !CanHappenNow(map)) return false;
            var care = DefDatabase<WorkTypeDef>.GetNamedSilentFail("Childcare");
            var crying = Round3Council.Thoughts("CryingBaby", "MyCryingBaby");
            if (care == null || crying.Count == 0) return false;
            var babies = map.mapPawns.AllPawnsSpawned.Where(p => p.Faction == Faction.OfPlayer && p.RaceProps.Humanlike && p.DevelopmentalStage.Baby() && !p.Dead).ToList();
            var grown = Round3Council.Grown(map);
            if (babies.Count == 0 || grown.Count < def.minColonists) return false;
            foreach (var a in grown.Where(p => def.roleA.Allows(p, null) && p.workSettings != null && !p.WorkTypeIsDisabled(care) && p.workSettings.GetPriority(care) > 0).InRandomOrder())
            {
                if (!Round3CouncilMath.Tired(a.needs?.rest?.CurLevelPercentage ?? 1f)) continue;
                var m = Round3Council.Memory(a, crying, Round3CouncilMath.CryingDays);
                if (m == null) continue;
                var baby = m.otherPawn != null && babies.Contains(m.otherPawn) ? m.otherPawn : babies.OrderBy(x => x.Position.DistanceToSquared(a.Position)).First();
                var b = grown.Where(p => p != a && !p.WorkTypeIsDisabled(care)).OrderByDescending(p => Round3Council.Opinion(p, a)).FirstOrDefault();
                if (b == null) continue;
                ctx = new Round2Context { def = def, map = map, a = a, b = b, c = baby };
                return true;
            }
            return false;
        }
    }

    // ===================================================================================================
    // After the tantrum
    // ===================================================================================================

    /// <summary>
    /// One of the colony (A) broke things in a tantrum in the last two days (the colony's deeds keep it), is themselves
    /// again and can do building work, and there is damage about the colony to mend (the game's own repairs). B is one of
    /// those who know of it, the least fond of A.
    /// </summary>
    public class DilemmaWorker_R3AfterTantrum : DilemmaWorker
    {
        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!CanHappenNow(map) || !map.listerBuildingsRepairable.RepairableBuildings(Faction.OfPlayer).Any()) return false;
            var grown = Round3Council.Grown(map);
            if (grown.Count < def.minColonists) return false;
            foreach (var a in grown.Where(p => def.roleA.Allows(p, null) && !p.WorkTypeIsDisabled(WorkTypeDefOf.Construction)).InRandomOrder())
            {
                var d = Round3Council.Tantrum(a);
                if (d == null) continue;
                var others = grown.Where(p => p != a).ToList();
                var b = others.Where(p => d.Knows(p.thingIDNumber)).OrderBy(p => Round3Council.Opinion(p, a)).FirstOrDefault()
                        ?? others.OrderBy(p => Round3Council.Opinion(p, a)).FirstOrDefault();
                if (b == null) continue;
                ctx = new DilemmaContext { def = def, map = map, a = a, b = b };
                return true;
            }
            return false;
        }
    }

    // ===================================================================================================
    // The scoffer's grave
    // ===================================================================================================

    /// <summary>
    /// One of the colony who never held to any god (a scoffer) died in the last six days and lies in a grave at home ({DEAD},
    /// the grave the question's place). B is the one who thought best of them; A one who holds a god (the priest, else the
    /// most devout), god1 A's god.
    /// </summary>
    public class DilemmaWorker_R3ScofferGrave : DilemmaWorker_Gods
    {
        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!Worship.On || !CanHappenNow(map)) return false;
            var grown = Round3Council.Grown(map);
            if (grown.Count < def.minColonists) return false;
            int now = Find.TickManager.TicksGame;
            foreach (var grave in map.listerBuildings.AllBuildingsColonistOfClass<Building_Grave>().ToList())
            {
                var body = grave.Corpse;
                var dead = body?.InnerPawn;
                if (dead == null || !dead.RaceProps.Humanlike || dead.Faction != Faction.OfPlayer) continue;
                if (!Round3CouncilMath.Lately(now, body.timeOfDeath, Round3CouncilMath.ScofferDays) || !Worship.Scoffer(dead)) continue;
                var b = grown.Where(p => Round3Council.Opinion(p, dead) >= Round3CouncilMath.FondFrom).OrderByDescending(p => Round3Council.Opinion(p, dead)).FirstOrDefault();
                if (b == null) continue;
                var a = GodSpeaker.Of(map, def.roleA);
                if (a == null || a == b)
                    a = grown.Where(p => p != b && def.roleA.Allows(p, null) && Worship.Ours(p) && !Worship.Scoffer(p) && Worship.GodOf(p) != null).OrderByDescending(Worship.Devout).FirstOrDefault();
                var g = a == null ? null : Worship.GodOf(a);
                if (g == null) continue;
                ctx = new Round2Context { def = def, map = map, a = a, b = b, dead = dead, place = grave, god1 = g.id };
                return true;
            }
            return false;
        }
    }

    // ===================================================================================================
    // The table without meat
    // ===================================================================================================

    /// <summary>
    /// Some of the colony hold a faith whose precepts forbid meat (Ideology), one of them (A) ate meat in the last two days
    /// (the game's own moods for it), and a stove of the colony's can cook Rimfolk's simple meal without meat. B is the best
    /// cook other than A; {COUNT} the meals the bill makes for the faithful.
    /// </summary>
    public class DilemmaWorker_R3NoMeatTable : DilemmaWorker
    {
        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!ModsConfig.IdeologyActive || !CanHappenNow(map)) return false;
            var recipe = DefDatabase<RecipeDef>.GetNamedSilentFail(Round3Council.VegRecipe);
            var stove = Round3Council.Bench(map, recipe);
            var ate = Round3Council.Thoughts("AteMeat_Abhorrent", "AteMeat_Horrible", "AteMeat_Disapproved");
            if (stove == null || ate.Count == 0) return false;
            var grown = Round3Council.Grown(map);
            if (grown.Count < def.minColonists) return false;
            var faithful = map.mapPawns.FreeColonistsSpawned.Where(p => p.RaceProps.Humanlike && !p.DevelopmentalStage.Baby() && Beliefs.Forbids(p, Belief.NoMeat)).ToList();
            foreach (var a in faithful.Where(p => grown.Contains(p) && def.roleA.Allows(p, null)).InRandomOrder())
            {
                if (Round3Council.Memory(a, ate, Round3CouncilMath.MeatDays) == null) continue;
                var b = VarietyThree.Best(grown, SkillDefOf.Cooking, VarietyThree.Work("Cooking"), a);
                if (b == null) continue;
                ctx = new DilemmaContextMore { def = def, map = map, a = a, b = b, place = stove, recipe = recipe, count = Round3CouncilMath.MealsFor(faithful.Count) };
                return true;
            }
            return false;
        }
    }
}
