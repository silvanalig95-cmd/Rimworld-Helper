using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// What a colonist's beliefs rule out, read from their ideoligion and their traits: lovin outside marriage, meat,
    /// plants that aren't fungus, nutrient paste, beer, lamps, clothes, a prosthetic, a hunt. Precepts are read by name
    /// (<see cref="BeliefMath"/>), by the flags the game gives them (darkness, nudity, slab beds) and by what they
    /// actually disapprove of (the game's events: lovin with someone not your spouse, eating meat, taking a drug), so a
    /// missing DLC or mod is simply never found, and a mod's own precept counts if it uses the game's events.
    /// <para/>
    /// Used by wishes (Wishes/: a wish the colonist's beliefs forbid is never raised, and one already voiced is let go
    /// quietly when they come to clash), life goals, council projects, and by everything that hands food or drink out
    /// (a feast's plates, a toast's cups): only what the colonist's food and drug policies allow, what wouldn't leave a
    /// bad "ate ..." memory, and nothing that would make them sick under Alpha Memes' rules.
    /// </summary>
    public static class Beliefs
    {
        // ---------------- What someone believes ----------------

        private struct Known
        {
            public int hour;
            public int ideo;
            public ulong mask;
        }

        private static readonly Dictionary<int, Known> known = new Dictionary<int, Known>();
        private static Game knownGame;

        /// <summary>Everything this colonist's beliefs rule out (worked out once an hour, or at once when their faith changes).</summary>
        public static ulong Mask(Pawn p)
        {
            if (p == null) return 0;
            if (Current.Game != knownGame)
            {
                known.Clear();
                knownGame = Current.Game;
            }
            int hour = (Find.TickManager?.TicksGame ?? 0) / GenDate.TicksPerHour;
            var ideo = IdeoOf(p);
            int ideoId = ideo?.id ?? -1;
            if (known.TryGetValue(p.thingIDNumber, out var k) && k.hour == hour && k.ideo == ideoId) return k.mask;
            ulong mask = Compute(p, ideo);
            if (known.Count > 400) known.Clear();
            known[p.thingIDNumber] = new Known { hour = hour, ideo = ideoId, mask = mask };
            return mask;
        }

        /// <summary>Their beliefs rule this out.</summary>
        public static bool Forbids(Pawn p, Belief b) => BeliefMath.Has(Mask(p), b);

        /// <summary>Their beliefs rule out any of these.</summary>
        public static bool ForbidsAny(Pawn p, ulong beliefs) => beliefs != 0 && BeliefMath.HasAny(Mask(p), beliefs);

        /// <summary>Their ideoligion holds this precept (looked up by name: a precept from a missing mod or DLC is never held).</summary>
        public static bool HasPrecept(Pawn p, string preceptDefName)
        {
            var ideo = IdeoOf(p);
            if (ideo == null || preceptDefName.NullOrEmpty()) return false;
            var def = DefDatabase<PreceptDef>.GetNamedSilentFail(preceptDefName);
            return def != null && ideo.HasPrecept(def);
        }

        private static Ideo IdeoOf(Pawn p)
        {
            if (p == null || !ModsConfig.IdeologyActive) return null;
            try
            {
                return p.Ideo;
            }
            catch
            {
                return null;
            }
        }

        private static ulong Compute(Pawn p, Ideo ideo)
        {
            ulong m = 0;
            try
            {
                if (ideo != null)
                {
                    var names = new List<KeyValuePair<string, string>>();
                    foreach (var precept in ideo.PreceptsListForReading)
                    {
                        var d = precept?.def;
                        if (d == null) continue;
                        names.Add(new KeyValuePair<string, string>(d.defName, d.issue?.defName));
                        if (d.prefersDarkness) m |= BeliefMath.Bit(Belief.Darkness);
                        if (d.prefersSlabBed) m |= BeliefMath.Bit(Belief.SlabBeds);
                        if (d.prefersNudity && (d.genderPrefersNudity == Gender.None || d.genderPrefersNudity == p.gender)) m |= BeliefMath.Bit(Belief.Nudity);
                    }
                    m |= BeliefMath.FromPrecepts(names);
                    // What the faith actually frowns on, whoever's precept it is.
                    if (Disapproves(p, ideo, HistoryEventDefOf.SharedBed_NonSpouse) || Disapproves(p, ideo, HistoryEventDefOf.GotLovin_NonSpouse))
                        m |= BeliefMath.Bit(Belief.LovinInMarriage);
                    if (Disapproves(p, ideo, HistoryEventDefOf.GotLovin_Spouse) || Disapproves(p, ideo, HistoryEventDefOf.SharedBed_Spouse))
                        m |= BeliefMath.Bit(Belief.NoLovin);
                    if (Disapproves(p, ideo, HistoryEventDefOf.AteMeat)) m |= BeliefMath.Bit(Belief.NoMeat);
                    if (Disapproves(p, ideo, HistoryEventDefOf.AteNonMeat)) m |= BeliefMath.Bit(Belief.MeatOnly);
                    if (Disapproves(p, ideo, HistoryEventDefOf.AteFungus)) m |= BeliefMath.Bit(Belief.NoFungus);
                    if (Disapproves(p, ideo, HistoryEventDefOf.AteNonFungusPlant)) m |= BeliefMath.Bit(Belief.FungusOnly);
                    if (Disapproves(p, ideo, HistoryEventDefOf.AteInsectMeatDirect)) m |= BeliefMath.Bit(Belief.NoInsectMeat);
                    if (Disapproves(p, ideo, HistoryEventDefOf.IngestedDrug)) m |= BeliefMath.Bit(Belief.NoDrugs);
                    if (Disapproves(p, ideo, HistoryEventDefOf.IngestedRecreationalDrug)) m |= BeliefMath.Bit(Belief.NoSocialDrugs);
                    if (Disapproves(p, ideo, HistoryEventDefOf.IngestedHardDrug)) m |= BeliefMath.Bit(Belief.NoHardDrugs);
                    if (Disapproves(p, ideo, HistoryEventDefOf.InstalledProsthetic)) m |= BeliefMath.Bit(Belief.NoProsthetics);
                    if (Disapproves(p, ideo, HistoryEventDefOf.KilledInnocentAnimal)) m |= BeliefMath.Bit(Belief.NoHunting);
                    if (Disapproves(p, ideo, HistoryEventDefOf.CutTree)) m |= BeliefMath.Bit(Belief.NoTreeCutting);
                    if (Disapproves(p, ideo, HistoryEventDefOf.ExecutedPrisoner, true)) m |= BeliefMath.Bit(Belief.NoExecution);
                    if (ideo.IdeoPrefersDarkness()) m |= BeliefMath.Bit(Belief.Darkness);
                    if (ideo.IdeoPrefersNudityForGender(p.gender)) m |= BeliefMath.Bit(Belief.Nudity);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("beliefs: precepts", e);
            }
            try
            {
                // Traits that say the same as a precept would.
                var traits = p.story?.traits;
                if (traits != null)
                {
                    if (traits.HasTrait(TraitDefOf.Nudist)) m |= BeliefMath.Bit(Belief.Nudity);
                    if (TraitDefOf.BodyPurist != null && traits.HasTrait(TraitDefOf.BodyPurist)) m |= BeliefMath.Bit(Belief.NoProsthetics);
                    // Teetotalers: no beer, no smokeleaf (the soft drinks a teetotaler may have are allowed by the drug's own flag).
                    if (TraitDefOf.DrugDesire != null && traits.HasTrait(TraitDefOf.DrugDesire) && traits.DegreeOfTrait(TraitDefOf.DrugDesire) < 0)
                        m |= BeliefMath.Bit(Belief.NoAlcohol);
                }
                if (p.WorkTagIsDisabled(WorkTags.Violent)) m |= BeliefMath.Bit(Belief.NoViolence);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("beliefs: traits", e);
            }
            return BeliefMath.Close(m);
        }

        /// <summary>
        /// Their ideoligion frowns on this: a precept makes them unwilling, or gives them a bad memory for doing it (or,
        /// with <paramref name="knowing"/>, for knowing it was done), and nothing about them cancels that memory.
        /// </summary>
        public static bool Disapproves(Pawn p, Ideo ideo, HistoryEventDef ev, bool knowing = false)
        {
            if (p == null || ideo == null || ev == null) return false;
            foreach (var precept in ideo.PreceptsListForReading)
            {
                var comps = precept?.def?.comps;
                if (comps == null) continue;
                foreach (var c in comps)
                {
                    if (c is PreceptComp_UnwillingToDo u)
                    {
                        if (u.eventDef == ev) return true;
                        continue;
                    }
                    ThoughtDef thought = null;
                    if (c is PreceptComp_SelfTookMemoryThought self && self.eventDef == ev) thought = self.thought;
                    else if (knowing && c is PreceptComp_KnowsMemoryThought knows && knows.eventDef == ev) thought = knows.thought;
                    if (thought == null || !Bad(thought)) continue;
                    if (!ThoughtUtility.ThoughtNullified(p, thought)) return true;
                }
            }
            return false;
        }

        /// <summary>A memory that lowers the mood (its first stage).</summary>
        public static bool Bad(ThoughtDef t)
        {
            var stages = t?.stages;
            if (stages == null) return false;
            for (int i = 0; i < stages.Count; i++)
                if (stages[i] != null) return stages[i].baseMoodEffect < 0f;
            return false;
        }

        // ---------------- Lovin ----------------

        public static bool Married(Pawn a, Pawn b) => a?.relations != null && b != null && a.relations.DirectRelationExists(PawnRelationDefOf.Spouse, b);

        /// <summary>These two sharing a bed goes against one of their faiths (lovin only within marriage, or none at all).</summary>
        public static bool SharingBedClashes(Pawn a, Pawn b) => a != null && b != null && BeliefMath.SharingBedClashes(Married(a, b), Mask(a), Mask(b));

        /// <summary>Partners not yet married, one of whose faiths keeps lovin to the married: a wedding means more to them.</summary>
        public static bool WaitingForMarriage(Pawn a, Pawn b) =>
            a != null && b != null && !Married(a, b) && (Forbids(a, Belief.LovinInMarriage) || Forbids(b, Belief.LovinInMarriage)) && !Forbids(a, Belief.NoLovin);

        // ---------------- Food and drink ----------------

        private static HashSet<ThingDef> pastes;

        /// <summary>The game's nutrient paste and Alpha Animals' and Alpha Memes' disgusting ones (by name; Alpha Memes reads the same three).</summary>
        public static bool IsPaste(ThingDef d)
        {
            if (d == null) return false;
            if (pastes == null)
            {
                pastes = new HashSet<ThingDef>();
                if (ThingDefOf.MealNutrientPaste != null) pastes.Add(ThingDefOf.MealNutrientPaste);
                foreach (var name in new[] { "AA_DisgustingMealNutrientPaste", "AM_DisgustingMealNutrientPaste" })
                {
                    var p = DefDatabase<ThingDef>.GetNamedSilentFail(name);
                    if (p != null) pastes.Add(p);
                }
            }
            return pastes.Contains(d);
        }

        public static bool IsAlcohol(ThingDef d) =>
            d?.ingestible != null && ((d.ingestible.foodType & FoodTypeFlags.Liquor) != 0 || (d.IsDrug && d.GetCompProperties<CompProperties_Drug>()?.chemical == ChemicalDefOf.Alcohol));

        private static bool IsInsectMeat(ThingDef d) => d?.ingestible?.sourceDef?.race != null && d.ingestible.sourceDef.race.FleshType == FleshTypeDefOf.Insectoid;

        private static bool IsMeat(ThingDef d) => d != null && (d.IsMeat || (d.ingestible != null && (d.ingestible.foodType & FoodTypeFlags.Meat) != 0));

        private static bool IsPlant(ThingDef d) => d?.ingestible != null && ((d.ingestible.foodType & FoodTypeFlags.VegetableOrFruit) != 0 || d.IsFungus);

        private static bool IsAnimalProduct(ThingDef d) => d != null && (d.IsAnimalProduct || (d.ingestible != null && (d.ingestible.foodType & FoodTypeFlags.AnimalProduct) != 0));

        /// <summary>What a kind of food is (no ingredients known: a meal is judged by kind alone).</summary>
        public static FoodFacts Facts(ThingDef d)
        {
            var f = new FoodFacts();
            if (d?.ingestible == null) return f;
            f.drug = d.IsDrug;
            f.water = d.defName == "DBH_WaterBottle";
            f.paste = IsPaste(d);
            f.alcohol = IsAlcohol(d);
            f.fungus = d.IsFungus;
            f.insect = IsInsectMeat(d);
            f.insectJelly = d == ThingDefOf.InsectJelly;
            f.meat = IsMeat(d) || f.insect;
            f.plant = IsPlant(d);
            f.animalProduct = IsAnimalProduct(d);
            f.simpleMeal = d.ingestible.preferability == FoodPreferability.MealSimple;
            f.rawBad = d.ingestible.preferability == FoodPreferability.RawBad;
            f.unknownIngredients = d.HasComp(typeof(CompIngredients));
            return f;
        }

        /// <summary>What this food is, with what went into it (read as the game and Alpha Memes read it).</summary>
        public static FoodFacts Facts(Thing t)
        {
            var d = FoodUtility.GetFinalIngestibleDef(t) ?? t?.def;
            var f = Facts(d);
            if (t == null || d?.ingestible == null) return f;
            f.unknownIngredients = false;
            var ingredients = t.TryGetComp<CompIngredients>()?.ingredients;
            if (ingredients != null)
            {
                foreach (var i in ingredients)
                {
                    if (i == null) continue;
                    if (i.IsFungus) f.fungus = true;
                    if (IsInsectMeat(i)) f.insect = true;
                    if (IsMeat(i)) f.meat = true;
                    if (IsPlant(i)) f.plant = true;
                    if (IsAnimalProduct(i)) f.animalProduct = true;
                }
            }
            try
            {
                if (FoodUtility.IsInsectCorpseOrInsectMeatIngredient(t)) f.insect = true;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("beliefs: insect food", e);
            }
            return f;
        }

        /// <summary>
        /// Eating this would give them a bad "ate ..." memory: the game's own list of what eating it does (their precepts,
        /// their traits, the meal's ingredients, a mod's additions to that list).
        /// </summary>
        public static bool BadMemoryFrom(Pawn p, Thing t)
        {
            if (p?.needs?.mood == null || t == null) return false;
            try
            {
                var thoughts = FoodUtility.ThoughtsFromIngesting(p, t, FoodUtility.GetFinalIngestibleDef(t) ?? t.def);
                if (thoughts == null) return false;
                for (int i = 0; i < thoughts.Count; i++)
                    if (Bad(thoughts[i].thought)) return true;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("beliefs: what eating does", e);
            }
            return false;
        }

        /// <summary>Their food policy lets them have it (drugs answer to the drug policy instead).</summary>
        public static bool FoodPolicyAllows(Pawn p, Thing t)
        {
            if (p == null || t == null || t.def.IsDrug) return true;
            try
            {
                var policy = p.foodRestriction?.GetCurrentRespectedRestriction(p);
                return policy == null || policy.Allows(t);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("beliefs: food policy", e);
                return true;
            }
        }

        /// <summary>
        /// A drug they may not take: their faith rules it out (all drugs, social ones, hard ones, alcohol), they're a
        /// teetotaler and it isn't a drink a teetotaler may have, their drug policy doesn't allow it for fun
        /// (<paramref name="forJoy"/>), or they're addicted to something and it's habit-forming.
        /// </summary>
        public static bool MindsDrug(Pawn p, ThingDef d, bool forJoy = true)
        {
            if (p == null || d == null || !d.IsDrug) return false;
            if (FaithForbidsDrug(p, d)) return true;
            var drug = d.GetCompProperties<CompProperties_Drug>();
            if (forJoy)
            {
                try
                {
                    var entry = p.drugs?.CurrentPolicy?[d];
                    if (entry != null && !entry.allowedForJoy) return true;
                }
                catch
                {
                    // Not in their policy at all (a drug added since): the policy says nothing about it.
                }
            }
            // Getting off something: nothing habit-forming, however small.
            if (drug != null && drug.Addictive && AddictionUtility.AddictedToAnything(p)) return true;
            return false;
        }

        /// <summary>
        /// Their beliefs alone rule this drug out: their faith (all drugs, social ones, hard ones, alcohol), or being a
        /// teetotaler when it isn't a drink a teetotaler may have.
        /// </summary>
        public static bool FaithForbidsDrug(Pawn p, ThingDef d)
        {
            if (p == null || d == null || !d.IsDrug) return false;
            ulong m = Mask(p);
            if (BeliefMath.Has(m, Belief.NoDrugs)) return true;
            if (d.IsNonMedicalDrug && BeliefMath.Has(m, Belief.NoSocialDrugs)) return true;
            if (d.ingestible?.drugCategory == DrugCategory.Hard && BeliefMath.Has(m, Belief.NoHardDrugs)) return true;
            var drug = d.GetCompProperties<CompProperties_Drug>();
            if (BeliefMath.Has(m, Belief.NoAlcohol) && IsAlcohol(d) && (drug == null || !drug.teetotalerCanConsume)) return true;
            var traits = p.story?.traits;
            return TraitDefOf.DrugDesire != null && traits != null && traits.HasTrait(TraitDefOf.DrugDesire) && traits.DegreeOfTrait(TraitDefOf.DrugDesire) < 0
                   && (drug == null || !drug.teetotalerCanConsume);
        }

        /// <summary>
        /// They won't, or shouldn't, eat or drink this thing now: the game says they won't, their food or drug policy
        /// doesn't allow it (<paramref name="policy"/>), it would leave a bad "ate ..." memory, or their beliefs say no
        /// (<see cref="BeliefMath.WhyNotEat"/>: Alpha Memes' fungus-and-insect rule, paste, a plain meal for one who
        /// expects better, alcohol). Used for whatever Rimfolk hands out to eat or drink.
        /// </summary>
        public static bool MindsEating(Pawn p, Thing t, bool policy = true) => WhyNotEat(p, t, policy) != null;

        /// <summary>Why they won't eat this (a short key), or null if they may.</summary>
        public static string WhyNotEat(Pawn p, Thing t, bool policy = true)
        {
            if (p == null || t?.def?.ingestible == null) return "inedible";
            try
            {
                // Food: would they eat it at all (their race, their genes, their title, their food policy)? A drink or a
                // drug answers to the drug policy instead (below).
                if (!t.def.IsDrug && !FoodUtility.WillEat(p, t)) return "wont";
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("beliefs: will eat", e);
                return "wont";
            }
            if (t.def.IsDrug)
            {
                if (MindsDrug(p, t.def, policy)) return "drug";
            }
            else if (policy && !FoodPolicyAllows(p, t)) return "policy";
            var f = Facts(t);
            f.badMemory = BadMemoryFrom(p, t);
            return BeliefMath.WhyNotEat(Mask(p), f);
        }

        /// <summary>
        /// A thing in store they'd count toward a wish for food (no memories asked for: raw food would always give one
        /// to someone who isn't going to eat it raw). Only their beliefs, by what it is and what went into it, and for a
        /// drink or a drug their drug policy too.
        /// </summary>
        public static bool FitForStock(Pawn p, Thing t)
        {
            if (p == null || t?.def?.ingestible == null) return true;
            if (t.def.IsDrug) return !MindsDrug(p, t.def);
            ulong m = Mask(p);
            if ((m & BeliefMath.Diet) == 0) return true;
            return BeliefMath.FitToEat(m, Facts(t));
        }

        /// <summary>A kind of food or drink they'd want stocked (judged by kind: a meal's ingredients aren't known yet).</summary>
        public static bool FitForStock(Pawn p, ThingDef d)
        {
            if (p == null || d?.ingestible == null) return true;
            if (d.IsDrug) return !MindsDrug(p, d);
            ulong m = Mask(p);
            if ((m & BeliefMath.Diet) == 0) return true;
            return BeliefMath.FitToEat(m, Facts(d));
        }

        /// <summary>
        /// At a feast with nothing in the stores fit for them: they sit with the others and eat nothing, and their
        /// journal may say so ({GATHERING}), in words that fit why (a diet their faith keeps, or just the stores).
        /// </summary>
        public static void NoteNothingFit(Pawn p, string gathering)
        {
            try
            {
                var comp = CouncilGameComponent.Instance;
                if (comp == null || p == null || !Rand.Chance(0.7f)) return;
                comp.journals.WriteTagged(p, "Council_Journal_NothingFit", new[] { "diet:" + DietTag(Mask(p)) }, null,
                    (gathering.NullOrEmpty() ? "CC_Setback_TheGathering".Translate().ToString() : gathering).Named("GATHERING"));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("beliefs: nothing fit to eat", e);
            }
        }

        /// <summary>The diet that most likely kept them from the table, for the journal's lines.</summary>
        public static string DietTag(ulong m)
        {
            if (BeliefMath.Has(m, Belief.FungusOrInsectOnly)) return "fungusinsect";
            if (BeliefMath.Has(m, Belief.NoAnimalProducts)) return "vegan";
            if (BeliefMath.Has(m, Belief.NoMeat)) return "vegetarian";
            if (BeliefMath.Has(m, Belief.MeatOnly)) return "carnivore";
            if (BeliefMath.Has(m, Belief.FungusOnly)) return "fungus";
            if (BeliefMath.Has(m, Belief.NoFungus)) return "nofungus";
            if (BeliefMath.Has(m, Belief.FineMealsOnly)) return "fine";
            return "none";
        }

        // ---------------- Wishes ----------------

        /// <summary>
        /// Their beliefs rule this wish out: one of the def's own <see cref="WishDef.forbiddenBy"/>, or what it asks for
        /// (food or drink they mustn't have, clothes for a nudist, a prosthetic for a body purist, a weapon for a pacifist,
        /// books for one who dislikes reading).
        /// </summary>
        public static bool AgainstWish(WishDef def, Pawn pawn, ThingDef thing)
        {
            if (def == null || pawn == null) return false;
            if (ForbidsAny(pawn, def.ForbiddenMask)) return true;
            return thing != null && AgainstThing(pawn, thing);
        }

        /// <summary>Asking for this thing goes against their beliefs.</summary>
        public static bool AgainstThing(Pawn pawn, ThingDef thing)
        {
            if (pawn == null || thing == null) return false;
            ulong m = Mask(pawn);
            if (thing.ingestible != null && !FitForStock(pawn, thing)) return true;
            if (thing.IsApparel)
            {
                if (BeliefMath.Has(m, Belief.Nudity) && thing.apparel != null && thing.apparel.countsAsClothingForNudity) return true;
                if (BeliefMath.Has(m, Belief.NoArmor) && thing.thingCategories != null && ThingCategoryDefOf.ApparelArmor != null
                    && thing.IsWithinCategory(ThingCategoryDefOf.ApparelArmor)) return true;
            }
            if (thing.isTechHediff && BeliefMath.Has(m, Belief.NoProsthetics) && !IsNaturalPart(thing)) return true;
            if (thing.IsWeapon && BeliefMath.Has(m, Belief.NoViolence)) return true;
            if (BeliefMath.Has(m, Belief.NoBooks) && thing.HasComp(typeof(CompBook))) return true;
            return false;
        }

        private static ThingCategoryDef naturalParts;
        private static bool naturalLooked;

        /// <summary>A natural organ or limb (a heart, an arm), which no faith that rejects prosthetics minds.</summary>
        private static bool IsNaturalPart(ThingDef d)
        {
            if (!naturalLooked)
            {
                naturalLooked = true;
                naturalParts = DefDatabase<ThingCategoryDef>.GetNamedSilentFail("BodyPartsNatural");
            }
            return naturalParts != null && d.thingCategories != null && d.IsWithinCategory(naturalParts);
        }
    }
}
