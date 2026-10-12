using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// "A place to do what I love": a passionate crafter wants a workshop, a cook a kitchen, a doctor a
    /// hospital, and so on. Only skills the colony can build for are considered.
    /// </summary>
    public class WishWorker_PassionSpace : WishWorker
    {
        private class Spec
        {
            public SkillDef skill;
            public Func<Map, bool> satisfied;
            public Func<Map, ThingDef> suggest;
            public string templateDefName;
            public string inspiration;
            /// <summary>Extra condition for the wish to make sense at all (e.g. a greenhouse only with frost ahead).</summary>
            public Func<Map, bool> needed;
        }

        /// <summary>
        /// A greenhouse only matters when the fields are about to freeze or already have: frost within about
        /// two weeks, or now, with enough of it left to matter. Never on maps that grow all year.
        /// </summary>
        public static bool GreenhouseSeason(Map map)
        {
            if (!SeasonUtil.HasSeasons(map)) return false;
            SeasonUtil.Winter(map, out float daysUntil, out float length, out bool now);
            if (daysUntil < 0f) return false;
            if (now) return length >= 6f;
            return daysUntil <= 12f && length >= 10f;
        }

        private static List<Spec> specs;

        private static List<Spec> Specs
        {
            get
            {
                if (specs != null) return specs;
                specs = new List<Spec>
                {
                    new Spec
                    {
                        skill = SkillDefOf.Crafting,
                        satisfied = m => StationInRole(m, SkillDefOf.Crafting, RoomUtil.Workshop),
                        suggest = m => Avail.BestBuildable(m, d => Avail.IsWorkstationFor(d, SkillDefOf.Crafting), "ElectricTailoringBench", "HandTailoringBench", "FueledSmithy", "CraftingSpot"),
                        templateDefName = "Council_Workshop",
                        inspiration = "Inspired_Creativity"
                    },
                    new Spec
                    {
                        skill = SkillDefOf.Artistic,
                        satisfied = m => StationInRole(m, SkillDefOf.Artistic, RoomUtil.Workshop),
                        suggest = m => Avail.BestBuildable(m, d => Avail.IsWorkstationFor(d, SkillDefOf.Artistic), "TableSculpting"),
                        templateDefName = "Council_Workshop",
                        inspiration = "Inspired_Creativity"
                    },
                    new Spec
                    {
                        skill = SkillDefOf.Cooking,
                        satisfied = m => RoomUtil.RoomsWithRole(m, RoomUtil.Kitchen).Any(r => RoomUtil.RoomContains(r, Avail.IsCookingStation)),
                        suggest = m => Avail.BestBuildable(m, Avail.IsCookingStation, "ElectricStove", "FueledStove"),
                        templateDefName = "Council_Kitchen",
                        inspiration = null
                    },
                    new Spec
                    {
                        skill = SkillDefOf.Intellectual,
                        satisfied = m => RoomUtil.RoomsWithRole(m, RoomUtil.Laboratory).Any(r => RoomUtil.RoomContains(r, Avail.IsResearchBench)),
                        suggest = m => Avail.BestBuildable(m, Avail.IsResearchBench, "HiTechResearchBench", "SimpleResearchBench"),
                        templateDefName = "Council_Laboratory",
                        inspiration = null
                    },
                    new Spec
                    {
                        skill = SkillDefOf.Medicine,
                        satisfied = m => RoomUtil.RoomsWithRole(m, RoomUtil.Hospital).Any(),
                        suggest = m => Avail.BestBuildable(m, Avail.IsSingleBed, "HospitalBed", "Bed"),
                        templateDefName = "Council_Hospital",
                        inspiration = "Inspired_Surgery"
                    },
                    new Spec
                    {
                        skill = SkillDefOf.Plants,
                        satisfied = m => RoomUtil.HasGreenhouse(m),
                        suggest = m => Avail.BestBuildable(m, Avail.IsHydroponics, "HydroponicsBasin") ?? Avail.BestBuildable(m, Avail.IsSunLamp, "SunLamp"),
                        templateDefName = "Council_Greenhouse",
                        inspiration = null,
                        needed = GreenhouseSeason
                    },
                    new Spec
                    {
                        skill = SkillDefOf.Animals,
                        satisfied = m => RoomUtil.RoomsWithRole(m, RoomUtil.Barn).Any(),
                        suggest = m => Avail.BestBuildable(m, Avail.IsAnimalBed, "AnimalBed", "AnimalSleepingBox", "AnimalSleepingSpot"),
                        templateDefName = "Council_Barn",
                        inspiration = "Inspired_Taming"
                    },
                };
                return specs;
            }
        }

        private static bool StationInRole(Map map, SkillDef skill, RoomRoleDef role)
        {
            foreach (var room in RoomUtil.RoomsWithRole(map, role))
            {
                if (RoomUtil.RoomContains(room, d => Avail.IsWorkstationFor(d, skill))) return true;
            }
            return false;
        }

        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            var map = HomeMap(pawn);
            if (map == null || pawn.skills == null) yield break;
            foreach (var spec in Specs)
            {
                if (spec.skill == null) continue;
                var rec = pawn.skills.GetSkill(spec.skill);
                if (rec == null || rec.TotallyDisabled || !People.Passionate(rec)) continue;
                if (spec.satisfied(map)) continue;
                if (spec.needed != null && !spec.needed(map)) continue;
                // Animals: only once the colony actually keeps animals.
                if (spec.skill == SkillDefOf.Animals && RoomUtil.ColonyAnimalCount(map) < 2) continue;
                // Medicine: a hospital needs at least two people to look after.
                if (spec.skill == SkillDefOf.Medicine && RoomUtil.ColonistCount(map) < 3) continue;
                var thing = spec.suggest(map);
                if (thing == null) continue;
                var c = Candidate(People.Burning(rec) ? 1.6f : 0.6f,
                    "CC_Reason_Passion".Translate(pawn.Named("PAWN"), spec.skill.label.Named("SKILL"),
                        (People.Burning(rec) ? "CC_PassionBurning" : "CC_PassionInterested").Translate().Named("PASSION")));
                c.thingDef = thing;
                c.subjectKey = spec.skill.defName;
                c.subjectLabel = ("CC_SkillSpace_" + spec.skill.defName).CanTranslate()
                    ? ("CC_SkillSpace_" + spec.skill.defName).Translate().Resolve()
                    : spec.skill.label;
                yield return c;
            }
        }

        private static Spec SpecFor(Wish wish) => Specs.FirstOrDefault(s => s.skill != null && s.skill.defName == wish.subjectKey);

        public override bool IsFulfilled(Wish wish)
        {
            var spec = SpecFor(wish);
            return spec == null || spec.satisfied(wish.Beneficiary.MapHeld);
        }

        public override bool StillValid(Wish wish)
        {
            var spec = SpecFor(wish);
            var map = wish.Beneficiary?.MapHeld;
            return spec == null || map == null || spec.needed == null || spec.needed(map);
        }

        public override RoomTemplateDef SuggestedTemplate(Wish wish)
        {
            var spec = SpecFor(wish);
            return spec == null ? null : DefDatabase<RoomTemplateDef>.GetNamedSilentFail(spec.templateDefName);
        }

        public override InspirationDef InspirationFor(Wish wish)
        {
            var spec = SpecFor(wish);
            return spec?.inspiration == null ? null : DefDatabase<InspirationDef>.GetNamedSilentFail(spec.inspiration);
        }

        public override string FulfilledWhen(Wish wish) => ("CC_When_Passion_" + wish.subjectKey).CanTranslate()
            ? ("CC_When_Passion_" + wish.subjectKey).Translate().Resolve()
            : base.FulfilledWhen(wish);
    }

    /// <summary>Passion for a skill whose work is switched off for them.</summary>
    public class WishWorker_LetMeWork : WishWorker
    {
        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            if (pawn.workSettings == null || !pawn.workSettings.EverWork || pawn.skills == null) yield break;
            foreach (var wt in DefDatabase<WorkTypeDef>.AllDefsListForReading)
            {
                if (wt.relevantSkills == null || wt.relevantSkills.Count == 0 || !wt.visible) continue;
                if (pawn.WorkTypeIsDisabled(wt) || pawn.workSettings.GetPriority(wt) > 0) continue;
                int best = 0;
                SkillDef bestSkill = null;
                foreach (var sk in wt.relevantSkills)
                {
                    int level = People.PassionLevel(pawn.skills.GetSkill(sk));
                    if (level > best)
                    {
                        best = level;
                        bestSkill = sk;
                    }
                }
                if (best == 0) continue;
                // Only if nothing else using that skill is turned on.
                bool otherOn = DefDatabase<WorkTypeDef>.AllDefsListForReading.Any(o => o != wt && o.relevantSkills != null
                    && o.relevantSkills.Contains(bestSkill) && !pawn.WorkTypeIsDisabled(o) && pawn.workSettings.GetPriority(o) > 0);
                if (otherOn) continue;
                var c = Candidate(best >= 2 ? 1.4f : 0.6f, "CC_Reason_WorkOff".Translate(pawn.Named("PAWN"), wt.gerundLabel.Named("WORK"), bestSkill.label.Named("SKILL")));
                c.subjectKey = wt.defName;
                c.subjectLabel = wt.gerundLabel;
                yield return c;
            }
        }

        public override bool IsFulfilled(Wish wish)
        {
            var wt = DefDatabase<WorkTypeDef>.GetNamedSilentFail(wish.subjectKey);
            return wt == null || wish.Beneficiary.workSettings == null || wish.Beneficiary.workSettings.GetPriority(wt) > 0;
        }

        public override bool StillValid(Wish wish)
        {
            var wt = DefDatabase<WorkTypeDef>.GetNamedSilentFail(wish.subjectKey);
            return wt != null && !wish.Beneficiary.WorkTypeIsDisabled(wt);
        }

        /// <summary>They just start doing it. You can switch it off again, if you dare.</summary>
        public override string TrySelfHelp(Wish wish)
        {
            var wt = DefDatabase<WorkTypeDef>.GetNamedSilentFail(wish.subjectKey);
            var ws = wish.Beneficiary.workSettings;
            if (wt == null || ws == null || wish.Beneficiary.WorkTypeIsDisabled(wt)) return null;
            ws.SetPriority(wt, 3);
            return "CC_SelfHelp_Work";
        }
    }

    /// <summary>Art where people gather: dining and rec rooms.</summary>
    public class WishWorker_Artwork : WishWorker
    {
        public static int SharedArtCount(Map map)
        {
            int n = 0;
            foreach (var room in RoomUtil.ColonyRooms(map))
            {
                if (room.Role != RoomUtil.DiningRoom && room.Role != RoomUtil.RecRoom) continue;
                n += RoomUtil.RoomCount(room, d => d.IsArt);
            }
            return n;
        }

        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            var map = HomeMap(pawn);
            if (map == null) yield break;
            var art = pawn.skills?.GetSkill(SkillDefOf.Artistic);
            bool artist = art != null && !art.TotallyDisabled && People.Passionate(art);
            bool greedy = HasTrait(pawn, TraitDefOf.Greedy);
            if (!artist && !greedy && RoomUtil.ExpectationOrder(pawn) < 4) yield break;
            if (!RoomUtil.ColonyRooms(map).Any(r => r.Role == RoomUtil.DiningRoom || r.Role == RoomUtil.RecRoom)) yield break;
            bool canSculpt = Avail.ColonyHas(map, d => Avail.IsWorkstationFor(d, SkillDefOf.Artistic))
                             || Avail.AnyBuildable(map, d => Avail.IsWorkstationFor(d, SkillDefOf.Artistic));
            if (!canSculpt) yield break;
            int count = SharedArtCount(map);
            if (count >= 3) yield break;
            var c = Candidate(artist ? 1.2f : 0.6f, artist ? "CC_Reason_Artist".Translate(pawn.Named("PAWN")) : "CC_Reason_BlandWalls".Translate(pawn.Named("PAWN")));
            c.baseline = count;
            c.subjectKey = "colony";
            yield return c;
        }

        public override bool IsFulfilled(Wish wish) => SharedArtCount(wish.Beneficiary.MapHeld) > wish.baseline;

        public override InspirationDef InspirationFor(Wish wish) => InspirationDefOf.Inspired_Creativity;
    }

    /// <summary>A fighter wants a weapon worth the name.</summary>
    public class WishWorker_BetterWeapon : WishWorker
    {
        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            var map = HomeMap(pawn);
            if (map == null || pawn.WorkTagIsDisabled(WorkTags.Violent) || pawn.skills == null || pawn.equipment == null) yield break;
            bool brawler = HasTrait(pawn, TraitDefOf.Brawler);
            var shoot = pawn.skills.GetSkill(SkillDefOf.Shooting);
            var melee = pawn.skills.GetSkill(SkillDefOf.Melee);
            bool wantsRanged = !brawler && shoot != null && People.Burning(shoot) && (melee == null || People.PassionLevel(shoot) >= People.PassionLevel(melee));
            bool wantsMelee = brawler || (melee != null && People.Burning(melee) && !wantsRanged);
            if (!wantsRanged && !wantsMelee) yield break;
            if (Satisfied(pawn, wantsRanged)) yield break;
            if (!WeaponObtainable(map, wantsRanged)) yield break;
            var c = Candidate(brawler && pawn.equipment.Primary != null && pawn.equipment.Primary.def.IsRangedWeapon ? 2f : 1.1f,
                brawler ? "CC_Reason_Brawler".Translate(pawn.Named("PAWN")) : "CC_Reason_Fighter".Translate(pawn.Named("PAWN")));
            c.subjectKey = wantsRanged ? "ranged" : "melee";
            c.subjectLabel = (wantsRanged ? "CC_RangedWeapon" : "CC_MeleeWeapon").Translate();
            yield return c;
        }

        private static bool Satisfied(Pawn pawn, bool ranged)
        {
            var prim = pawn.equipment?.Primary;
            if (prim == null) return false;
            if (ranged != prim.def.IsRangedWeapon) return false;
            if (!prim.TryGetQuality(out var q)) return true;
            return q >= QualityCategory.Normal;
        }

        private static bool WeaponObtainable(Map map, bool ranged)
        {
            foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.Weapon))
            {
                if (t.def.IsRangedWeapon != ranged || t.IsForbidden(Faction.OfPlayer) || !t.def.IsWeapon) continue;
                if (t.TryGetQuality(out var q) && q < QualityCategory.Normal) continue;
                return true;
            }
            foreach (var r in DefDatabase<RecipeDef>.AllDefsListForReading)
            {
                var prod = r.ProducedThingDef;
                if (prod == null || !prod.IsWeapon || prod.IsRangedWeapon != ranged || !r.AvailableNow) continue;
                if (Avail.RecipeUsers(r).Any(u => map.listerBuildings.ColonistsHaveBuilding(u) || Avail.CanBuild(u, map))) return true;
            }
            return false;
        }

        public override bool IsFulfilled(Wish wish) => Satisfied(wish.Beneficiary, wish.subjectKey == "ranged");
    }

    /// <summary>A brewer's thirst: a fermenting barrel, or beer in the stockroom.</summary>
    public class WishWorker_Brewery : WishWorker
    {
        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            var map = HomeMap(pawn);
            if (map == null || TraitDegree(pawn, TraitDefOf.DrugDesire) <= 0) yield break;
            if (Avail.ColonyHas(map, Avail.IsFermenter)) yield break;
            var barrel = Avail.BestBuildable(map, Avail.IsFermenter, "FermentingBarrel");
            if (barrel == null) yield break;
            var c = Candidate(1.3f, "CC_Reason_Chemical".Translate(pawn.Named("PAWN")));
            c.thingDef = barrel;
            c.subjectKey = "colony";
            yield return c;
        }

        public override bool IsFulfilled(Wish wish)
        {
            var map = wish.Beneficiary.MapHeld;
            if (Avail.ColonyHas(map, Avail.IsFermenter)) return true;
            // Or a stockroom with something to drink: beer, mead, wine, whatever this colony's mods brew.
            return WishWorker_LittleSomething.Stock(map) >= 5;
        }
    }

    /// <summary>A gourmand (or anyone sick of raw food) wants proper meals.</summary>
    public class WishWorker_BetterFood : WishWorker
    {
        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            var map = HomeMap(pawn);
            if (map == null) yield break;
            var gourmand = DefDatabase<TraitDef>.GetNamedSilentFail("Gourmand");
            // Fussy eaters from trait mods count too (the "gourmet" tag group).
            bool trueGourmand = HasTrait(pawn, gourmand);
            bool isGourmand = trueGourmand || PeopleTags.Has(pawn, "gourmet");
            bool ateRaw = HasMemory(pawn, ThoughtDefOf.AteRawFood) || HasMemory(pawn, "AteNutrientPasteMeal");
            bool particular = Standards.Of(pawn) >= 0.6f && !HasTrait(pawn, TraitDefOf.Ascetic);
            if (!isGourmand && !ateRaw && !particular) yield break;
            var want = ateRaw && !isGourmand && !particular ? FoodPreferability.MealSimple : FoodPreferability.MealFine;
            if (!CanCook(map, want)) yield break;
            if (MealStock(map, want) >= RoomUtil.ColonistCount(map) * 2) yield break;
            var c = Candidate(isGourmand ? 1.4f : 1.1f, isGourmand ? (trueGourmand ? "CC_Reason_Gourmand" : "CC_PM_Reason_Gourmet").Translate(pawn.Named("PAWN"))
                : ateRaw ? "CC_Reason_AteRaw".Translate(pawn.Named("PAWN")) : "CC_Reason_StandardsFood".Translate(pawn.Named("PAWN")));
            c.subjectKey = want.ToString();
            c.subjectLabel = (want == FoodPreferability.MealFine ? "CC_FineMeals" : "CC_ProperMeals").Translate();
            c.targetValue = RoomUtil.ColonistCount(map) * 2;
            yield return c;
        }

        public static bool CanCook(Map map, FoodPreferability pref)
        {
            foreach (var r in DefDatabase<RecipeDef>.AllDefsListForReading)
            {
                var prod = r.ProducedThingDef;
                if (prod?.ingestible == null || prod.ingestible.preferability != pref || !r.AvailableNow) continue;
                if (Avail.RecipeUsers(r).Any(u => map.listerBuildings.ColonistsHaveBuilding(u) || Avail.CanBuild(u, map))) return true;
            }
            return false;
        }

        public static int MealStock(Map map, FoodPreferability atLeast)
        {
            int n = 0;
            foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.FoodSourceNotPlantOrTree))
            {
                if (t.def.ingestible != null && t.def.ingestible.preferability >= atLeast && t.def.ingestible.preferability <= FoodPreferability.MealLavish
                    && !t.IsForbidden(Faction.OfPlayer)) n += t.stackCount;
            }
            return n;
        }

        public override bool IsFulfilled(Wish wish)
        {
            var pref = wish.subjectKey == FoodPreferability.MealSimple.ToString() ? FoodPreferability.MealSimple : FoodPreferability.MealFine;
            return MealStock(wish.Beneficiary.MapHeld, pref) >= wish.targetValue
                   || (pref == FoodPreferability.MealFine && HasMemory(wish.Beneficiary, "AteFineMeal"))
                   || HasMemory(wish.Beneficiary, "AteLavishMeal");
        }

        public override string ProgressText(Wish wish)
        {
            var pref = wish.subjectKey == FoodPreferability.MealSimple.ToString() ? FoodPreferability.MealSimple : FoodPreferability.MealFine;
            return "CC_Progress_Meals".Translate(MealStock(wish.Beneficiary.MapHeld, pref), wish.targetValue.ToString("0"));
        }
    }

    /// <summary>A cook wants cold storage so the harvest doesn't rot.</summary>
    public class WishWorker_Freezer : WishWorker
    {
        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            var map = HomeMap(pawn);
            if (map == null || RoomUtil.HasFreezer(map)) yield break;
            var cook = pawn.skills?.GetSkill(SkillDefOf.Cooking);
            bool cookPassion = cook != null && !cook.TotallyDisabled && People.Passionate(cook);
            bool ateRotten = HasMemory(pawn, ThoughtDefOf.AteRottenFood);
            if (!cookPassion && !ateRotten) yield break;
            var cooler = Avail.BestBuildable(map, d => d.thingClass != null && typeof(Building_Cooler).IsAssignableFrom(d.thingClass), "Cooler");
            if (cooler == null) yield break;
            var c = Candidate(ateRotten ? 1.6f : 1f, ateRotten ? "CC_Reason_AteRotten".Translate(pawn.Named("PAWN")) : "CC_Reason_CookFreezer".Translate(pawn.Named("PAWN")));
            c.thingDef = cooler;
            c.subjectKey = "colony";
            yield return c;
        }

        public override bool IsFulfilled(Wish wish) => RoomUtil.HasFreezer(wish.Beneficiary.MapHeld);
    }
}
