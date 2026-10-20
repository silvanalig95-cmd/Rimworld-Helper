using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>
    /// Everyday pressures: the small, fixable things that wear on people when a colony is run without care. The same
    /// meal again, the same work day after day, someone they love away for days, a dining or rec room fuller than it
    /// can hold, and half-built work left standing. Each is mild, says what causes it and what helps, and fades once
    /// that's dealt with; the "Life's ups and downs" setting scales them. Worked out once an hour (meals as they're
    /// eaten) by <see cref="PressureTracker"/>; the moods are situational thoughts that only read what it found.
    /// </summary>
    public static class Pressures
    {
        private static CouncilSettings S => ColonyCouncilMod.Settings;

        public static bool On => S != null && S.MoodEffects;

        /// <summary>The setting's strength for a pressure's bad side.</summary>
        public static float LifeFactor => On ? PressureMath.Factor(S.lifePressure) : 0f;

        /// <summary>Gentle leaves out the fussiest ones: half-built work, and the standards' contrast.</summary>
        public static bool Gentle => S != null && S.lifePressure == LifePressure.Gentle;

        /// <summary>
        /// Contrast: the comfortable feel the colony's own frictions more. The standards' factor for Rimfolk's bad moods
        /// that weren't scaled when they were given (moments, frictions) and for the pressures; off in gentle.
        /// </summary>
        public static float Contrast(Pawn p) => Gentle || p == null ? 1f : Standards.MoodFactor(p, false);

        /// <summary>A small letdown the council never gave (a moment, a friction) as it's felt: the setting's softening, the contrast and hard times.</summary>
        public static float Letdown(Pawn p) =>
            (S == null ? 1f : PressureMath.LetdownFactor(S.lifePressure)) * Contrast(p) * PressureMath.HardTimesLetdown(Hard(p));

        /// <summary>How near breaking someone is, 0 to 1 (<see cref="PressureMath.HardTimes"/>).</summary>
        public static float Hard(Pawn p)
        {
            var mood = p?.needs?.mood;
            var breaker = p?.mindState?.mentalBreaker;
            if (mood == null || breaker == null) return 0f;
            return PressureMath.HardTimes(mood.CurLevelPercentage, breaker.BreakThresholdMinor);
        }

        /// <summary>A pressure's bad mood: the setting and the colonist's standards, never less than a whole point.</summary>
        public static float Bad(Pawn p, float offset) => PressureMath.Scale(offset, LifeFactor * Contrast(p) * PressureMath.HardTimesPressure(Hard(p)));

        public static PressureTracker Tracker => CouncilGameComponent.Instance?.pressures;

        /// <summary>Who feels these: the colony's free people with an inner life, not babies, slaves or quest lodgers.</summary>
        public static bool Eligible(Pawn p) =>
            p != null && !p.Dead && p.IsFreeNonSlaveColonist && !p.IsQuestLodger() && p.needs?.mood != null && People.HasInnerLife(p)
            && !p.DevelopmentalStage.Baby();

        // ---------------- Traits (read by name: a mod's trait that isn't there is never matched) ----------------

        private static bool looked;
        private static TraitDef gourmand, refinedPalate, gastronomist, workaholic, recluse, schizoid, slob, neat, perfectionist;

        private static void Look()
        {
            if (looked) return;
            looked = true;
            gourmand = DefDatabase<TraitDef>.GetNamedSilentFail("Gourmand");
            refinedPalate = DefDatabase<TraitDef>.GetNamedSilentFail("VTE_RefinedPalate");
            gastronomist = DefDatabase<TraitDef>.GetNamedSilentFail("VTE_Gastronomist");
            workaholic = DefDatabase<TraitDef>.GetNamedSilentFail("VTE_Workaholic");
            recluse = DefDatabase<TraitDef>.GetNamedSilentFail("Recluse");
            schizoid = DefDatabase<TraitDef>.GetNamedSilentFail("VTE_Schizoid");
            slob = DefDatabase<TraitDef>.GetNamedSilentFail("VTE_Slob");
            neat = DefDatabase<TraitDef>.GetNamedSilentFail("VTE_Neat");
            perfectionist = DefDatabase<TraitDef>.GetNamedSilentFail("VTE_Perfectionist");
        }

        private static bool Has(Pawn p, TraitDef t) => t != null && (p.story?.traits?.HasTrait(t) ?? false);

        private static int Industry(Pawn p) =>
            TraitDefOf.Industriousness != null && (p.story?.traits?.HasTrait(TraitDefOf.Industriousness) ?? false) ? p.story.traits.DegreeOfTrait(TraitDefOf.Industriousness) : 0;

        /// <summary>Ascetics eat what there is and don't think about it.</summary>
        public static bool AnyFood(Pawn p) => Has(p, TraitDefOf.Ascetic);

        /// <summary>Gourmands (and Vanilla Traits Expanded's refined palates and gastronomists) mind the same meal more.</summary>
        public static bool Picky(Pawn p)
        {
            Look();
            return Has(p, gourmand) || Has(p, refinedPalate) || Has(p, gastronomist);
        }

        /// <summary>
        /// The same work doesn't wear on the hard-working and industrious, workaholics, children, or anyone with a burning
        /// passion for that work.
        /// </summary>
        public static bool SparedSameWork(Pawn p, WorkTypeDef work)
        {
            Look();
            if (Industry(p) >= 1 || Has(p, workaholic) || p.DevelopmentalStage.Juvenile()) return true;
            if (work?.relevantSkills == null || p.skills == null) return false;
            foreach (var s in work.relevantSkills)
                if (s != null && People.Burning(p.skills.GetSkill(s)))
                    return true;
            return false;
        }

        /// <summary>Any passion for one of the work's skills (a burning one spares them altogether: <see cref="SparedSameWork"/>).</summary>
        public static bool PassionFor(Pawn p, WorkTypeDef work)
        {
            if (work?.relevantSkills == null || p?.skills == null) return false;
            foreach (var s in work.relevantSkills)
                if (s != null && People.Passionate(p.skills.GetSkill(s)))
                    return true;
            return false;
        }

        /// <summary>Psychopaths and recluses don't pine for friends (a partner is another matter).</summary>
        public static bool SparedMissingFriends(Pawn p)
        {
            Look();
            return Has(p, TraitDefOf.Psychopath) || Has(p, recluse);
        }

        /// <summary>Recluses and Vanilla Traits Expanded's schizoids take a crowd a step harder.</summary>
        public static bool HatesCrowds(Pawn p)
        {
            Look();
            return Has(p, recluse) || Has(p, schizoid);
        }

        /// <summary>The lazy and slothful, and slobs, don't care about half-built work.</summary>
        public static bool SparedUnfinished(Pawn p)
        {
            Look();
            return Industry(p) < 0 || Has(p, slob);
        }

        /// <summary>The industrious, the neat and perfectionists mind half-built work a step more.</summary>
        public static bool MindsUnfinished(Pawn p)
        {
            Look();
            return Industry(p) >= 2 || Has(p, neat) || Has(p, perfectionist);
        }

        // ---------------- Food ----------------

        /// <summary>
        /// Food that counts for the same meal and a varied table: meals and food good to eat raw (berries, pemmican,
        /// cheese). Not drugs, not treats eaten for fun, and not what the game already minds the taste of (raw meat and
        /// vegetables, nutrient paste, kibble), so a bad meal is never counted twice.
        /// </summary>
        public static bool CountsAsFood(ThingDef d)
        {
            var ing = d?.ingestible;
            if (ing == null || d.IsDrug || !d.IsNutritionGivingIngestible) return false;
            var pref = ing.preferability;
            if (pref < FoodPreferability.RawTasty || pref > FoodPreferability.MealLavish || pref == FoodPreferability.MealAwful) return false;
            if (ing.joy > 0f && pref < FoodPreferability.MealAwful) return false;
            var taste = ing.tasteThought?.stages;
            return taste.NullOrEmpty() || taste[0] == null || taste[0].baseMoodEffect >= 0f;
        }

        /// <summary>A remembered meal's name: its kind ("simple meal"), whatever went into it.</summary>
        public static string FoodLabel(string key)
        {
            string defName = PressureMath.KindOf(key);
            if (defName.NullOrEmpty()) return "?";
            return DefDatabase<ThingDef>.GetNamedSilentFail(defName)?.label ?? defName;
        }

        public static string WorkLabel(string defName)
        {
            var w = defName.NullOrEmpty() ? null : DefDatabase<WorkTypeDef>.GetNamedSilentFail(defName);
            return w == null ? "CC_Pressure_WorkFallback".Translate().ToString() : (w.gerundLabel ?? w.labelShort ?? w.label);
        }

        // ---------------- Text ----------------

        /// <summary>Fills a pressure's label or description: {FOOD}, {FOODS} (plural), {WORK}, {OTHER_nameDef} (the one missed), {ROOM}, {THING}.</summary>
        public static string Fill(string raw, Pawn p)
        {
            if (raw.NullOrEmpty() || raw.IndexOf('{') < 0) return raw;
            var t = Tracker;
            var rec = t?.Peek(p);
            var u = p?.MapHeld != null ? t?.UnfinishedOn(p.MapHeld) : null;
            string food = rec != null ? FoodLabel(rec.foodKind) : "?";
            string foods = rec != null ? Find.ActiveLanguageWorker.Pluralize(food) : "?";
            string work = rec != null ? WorkLabel(rec.streakWork) : "?";
            string room = rec?.crowdRoom ?? "CC_Pressure_RoomFallback".Translate().ToString();
            string thing = u?.thing ?? "CC_Pressure_ThingFallback".Translate().ToString();
            var other = rec?.missed != null ? rec.missed.Named("OTHER") : "?".Named("OTHER");
            return raw.Formatted(food.Named("FOOD"), foods.Named("FOODS"), work.Named("WORK"), other, room.Named("ROOM"), thing.Named("THING"),
                p.Named("PAWN")).Resolve();
        }
    }

    /// <summary>One colonist's side of the everyday pressures, kept in Rimfolk's save data.</summary>
    public class PawnPressure : IExposable
    {
        public Pawn pawn;
        /// <summary>The food they ate lately (defNames), oldest first, and when.</summary>
        public List<string> meals = new List<string>();
        public List<int> mealTicks = new List<int>();
        /// <summary>The day being counted, and what kind of work each hour of it went to (work type defNames, counts).</summary>
        public int workDay = -1;
        public List<string> workKinds = new List<string>();
        public List<int> workCounts = new List<int>();
        /// <summary>The kind of work that has filled their days lately, and for how many days running.</summary>
        public string streakWork;
        public int streakDays;

        // Worked out from the above and from the hour's look around: not saved.
        public bool fresh;
        public int foodStage;
        public bool varied;
        public string foodKind;
        public int foodCount, foodOf, kindsLately;
        public int workStage;
        public Pawn missed;
        public bool missedPartner;
        public int missedStage;
        public int missedTicks;
        public int crowdTick = -999999;
        public int crowdLevel;
        public string crowdRoom;
        public int crowdPeople, crowdSeats = -1, crowdCells;

        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Collections.Look(ref meals, "meals", LookMode.Value);
            Scribe_Collections.Look(ref mealTicks, "mealTicks", LookMode.Value);
            Scribe_Values.Look(ref workDay, "workDay", -1);
            Scribe_Collections.Look(ref workKinds, "workKinds", LookMode.Value);
            Scribe_Collections.Look(ref workCounts, "workCounts", LookMode.Value);
            Scribe_Values.Look(ref streakWork, "streakWork");
            Scribe_Values.Look(ref streakDays, "streakDays");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (meals == null || mealTicks == null || meals.Count != mealTicks.Count)
                {
                    meals = new List<string>();
                    mealTicks = new List<int>();
                }
                if (workKinds == null || workCounts == null || workKinds.Count != workCounts.Count)
                {
                    workKinds = new List<string>();
                    workCounts = new List<int>();
                }
            }
        }
    }

    /// <summary>How much half-built work stands in one home map's home area, this hour.</summary>
    public class Unfinished
    {
        public int frames;
        public int blueprints;
        public int stage;
        /// <summary>What most of it is (a wall, a door), for the mood's label.</summary>
        public string thing;
    }

    /// <summary>
    /// Keeps the everyday pressures: once an hour it notes everyone's work, who is away from whom, which shared rooms
    /// are too full and how long half-built work has stood; meals are noted as they're eaten (Patch_Ate). Nothing here
    /// scans per tick, and everything is pruned as people and things go.
    /// </summary>
    public class PressureTracker : IExposable
    {
        public List<PawnPressure> people = new List<PawnPressure>();
        /// <summary>Two people apart ("ThingID|ThingID", in order): when they were first seen apart.</summary>
        private Dictionary<string, int> apartSince = new Dictionary<string, int>();
        /// <summary>Half-built work in the home area, by thing id: when first seen, and when a frame last moved forward and how far it was.</summary>
        private Dictionary<int, int> firstSeen = new Dictionary<int, int>();
        private Dictionary<int, int> lastProgress = new Dictionary<int, int>();
        private Dictionary<int, float> lastWork = new Dictionary<int, float>();

        private readonly Dictionary<Pawn, PawnPressure> byPawn = new Dictionary<Pawn, PawnPressure>();
        private readonly Dictionary<int, Unfinished> unfinished = new Dictionary<int, Unfinished>();
        private readonly HashSet<string> touchedPairs = new HashSet<string>();
        private readonly HashSet<int> seenThings = new HashSet<int>();
        private int mealOptions = 1;
        private int optionsDay = -1;
        private int pruneDay = -1;

        private static CouncilSettings S => ColonyCouncilMod.Settings;
        private static int Now => Find.TickManager.TicksGame;
        private const int CrowdLingerTicks = GenDate.TicksPerHour * 12;

        public PawnPressure For(Pawn p, bool create)
        {
            if (p == null) return null;
            if (byPawn.Count != people.Count) Reindex();
            if (byPawn.TryGetValue(p, out var rec)) return rec;
            if (!create) return null;
            rec = new PawnPressure { pawn = p };
            people.Add(rec);
            byPawn[p] = rec;
            return rec;
        }

        private void Reindex()
        {
            byPawn.Clear();
            people.RemoveAll(r => r?.pawn == null);
            foreach (var r in people) byPawn[r.pawn] = r;
            if (byPawn.Count != people.Count)
            {
                // Two records for one colonist (shouldn't happen): keep the first.
                var seen = new HashSet<Pawn>();
                people.RemoveAll(r => !seen.Add(r.pawn));
            }
        }

        /// <summary>A colonist's record with its derived values worked out (after a load, before the first hour).</summary>
        public PawnPressure Peek(Pawn p)
        {
            var rec = For(p, false);
            if (rec != null && !rec.fresh)
            {
                rec.fresh = true;
                RefreshFood(rec, Now);
                RefreshWork(rec);
            }
            return rec;
        }

        public Unfinished UnfinishedOn(Map map) => map != null && unfinished.TryGetValue(map.uniqueID, out var u) ? u : null;

        /// <summary>The crowding mood still lingers this long after the crowded hour.</summary>
        public static bool CrowdedLately(PawnPressure rec) => rec != null && rec.crowdLevel > 0 && Now - rec.crowdTick < CrowdLingerTicks;

        // ---------------- Every hour ----------------

        public void Hourly(CouncilGameComponent comp)
        {
            if (!Pressures.On) return;
            int now = Now;
            int day = now / GenDate.TicksPerDay;
            var everyone = PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists_NoCryptosleep;
            for (int i = 0; i < everyone.Count; i++)
            {
                var p = everyone[i];
                if (!Pressures.Eligible(p)) continue;
                var rec = For(p, true);
                try
                {
                    NoteWork(rec, p, day);
                    RefreshFood(rec, now);
                    RefreshWork(rec);
                    rec.fresh = true;
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("pressures for a colonist", e);
                }
            }
            Safe("missing someone", () => Missing(comp, everyone, now));
            foreach (var map in Find.Maps)
            {
                if (!map.IsPlayerHome) continue;
                Safe("crowded rooms", () => Crowding(map, now));
                Safe("half-built work", () => Construction(map, now));
            }
            Safe("half-built work", PruneConstruction);
            if (day != pruneDay)
            {
                pruneDay = day;
                people.RemoveAll(r => r?.pawn == null || !People.StillOurs(r.pawn));
                Reindex();
            }
        }

        private static void Safe(string context, Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("pressures: " + context, e);
            }
        }

        // ---------------- Meals ----------------

        /// <summary>A colonist ate something (Patch_Ate): remembered with what went into it.</summary>
        public void Notify_Ate(Pawn p, Thing meal)
        {
            var food = meal?.def;
            if (food == null || !Pressures.On || !Pressures.Eligible(p) || !Pressures.CountsAsFood(food)) return;
            var rec = For(p, true);
            int now = Now;
            var ingredients = meal.TryGetComp<CompIngredients>()?.ingredients;
            rec.meals.Add(PressureMath.MealKey(food.defName, ingredients?.Where(d => d != null).Select(d => d.defName)));
            rec.mealTicks.Add(now);
            RefreshFood(rec, now);
        }

        /// <summary>A remembered meal that's a fine meal or better (the game's own, or a mod's at that level).</summary>
        private static bool FineOrBetter(string key)
        {
            var def = key == null ? null : DefDatabase<ThingDef>.GetNamedSilentFail(PressureMath.KindOf(key));
            return def?.ingestible != null && def.ingestible.preferability >= FoodPreferability.MealFine;
        }

        /// <summary>How many different kinds of meal the colony can cook right now (worked out once a day).</summary>
        public int MealOptions
        {
            get
            {
                int day = Now / GenDate.TicksPerDay;
                if (day != optionsDay)
                {
                    optionsDay = day;
                    try
                    {
                        mealOptions = CountMealOptions();
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("pressures: meal options", e);
                    }
                }
                return mealOptions;
            }
        }

        /// <summary>
        /// The kinds of meal the colony's recipes make that it can cook now: researched, with a bench or fire for it in a
        /// home, and a cook skilled enough. Only real meals count as a choice: colonists pick a meal over travel food
        /// like pemmican whenever there is one, so that's no way to vary what they eat (eaten, it still counts as
        /// something different). A camp with a fire and simple meals has one; a kitchen with fine meals more.
        /// </summary>
        private static int CountMealOptions()
        {
            var maps = Find.Maps.Where(m => m.IsPlayerHome).ToList();
            if (maps.Count == 0) return 1;
            var cooks = PawnsFinder.AllMaps_FreeColonistsSpawned.Where(c => c.MapHeld != null && c.MapHeld.IsPlayerHome).ToList();
            var kinds = new HashSet<ThingDef>();
            foreach (var r in DefDatabase<RecipeDef>.AllDefsListForReading)
            {
                ThingDef prod;
                try
                {
                    prod = r.ProducedThingDef;
                }
                catch
                {
                    continue;
                }
                if (prod == null || kinds.Contains(prod) || !Pressures.CountsAsFood(prod) || prod.ingestible.preferability < FoodPreferability.MealSimple
                    || !r.AvailableNow) continue;
                bool bench = false;
                foreach (var u in r.AllRecipeUsers)
                {
                    if (u == null || u.category != ThingCategory.Building) continue;
                    if (maps.Any(m => m.listerBuildings.ColonistsHaveBuilding(u)))
                    {
                        bench = true;
                        break;
                    }
                }
                if (!bench) continue;
                if (!r.skillRequirements.NullOrEmpty() && !cooks.Any(r.PawnSatisfiesSkillRequirements)) continue;
                kinds.Add(prod);
            }
            return Math.Max(1, kinds.Count);
        }

        private void RefreshFood(PawnPressure rec, int now)
        {
            // Only the last week's meals, and only so many.
            int cutoff = now - (int)(PressureMath.MealWindowDays * GenDate.TicksPerDay);
            while (rec.meals.Count > 0 && (rec.meals.Count > PressureMath.MealsKept || rec.mealTicks[0] < cutoff))
            {
                rec.meals.RemoveAt(0);
                rec.mealTicks.RemoveAt(0);
            }
            rec.foodStage = 0;
            rec.varied = false;
            rec.foodKind = null;
            var p = rec.pawn;
            if (p == null || Pressures.AnyFood(p) || rec.meals.Count == 0) return;
            // The kind that came up most in the last eight, and how often in all of the last week.
            int from = Math.Max(0, rec.meals.Count - 8), best = 0;
            for (int i = from; i < rec.meals.Count; i++)
            {
                int n = 0;
                for (int j = from; j < rec.meals.Count; j++)
                    if (rec.meals[j] == rec.meals[i]) n++;
                if (n > best)
                {
                    best = n;
                    rec.foodKind = rec.meals[i];
                }
            }
            int options = MealOptions;
            int stage = PressureMath.FoodStage(rec.meals, options);
            if (stage > 0 && Pressures.Picky(p)) stage = PressureMath.Picky(stage, options);
            // A fine or lavish meal every day is eating well, not a rut: nobody tires of it enough to mind.
            if (stage > 0 && FineOrBetter(rec.foodKind)) stage = 0;
            rec.foodStage = stage;
            rec.foodOf = rec.meals.Count;
            rec.foodCount = rec.meals.Count(m => m == rec.foodKind);
            int since = now - (int)(PressureMath.VarietyWindowDays * GenDate.TicksPerDay);
            var recent = new List<string>();
            for (int i = 0; i < rec.meals.Count; i++)
                if (rec.mealTicks[i] >= since) recent.Add(rec.meals[i]);
            rec.kindsLately = recent.Select(PressureMath.KindOf).Distinct().Count();
            rec.varied = stage == 0 && PressureMath.VariedTable(recent);
        }

        // ---------------- Work ----------------

        /// <summary>Work that isn't a choice of routine: putting out fires, and lying in bed as a patient.</summary>
        private static readonly HashSet<string> NotRoutine = new HashSet<string> { "Firefighter", "Patient", "PatientBedRest" };

        private static void NoteWork(PawnPressure rec, Pawn p, int day)
        {
            if (rec.workDay != day)
            {
                if (rec.workDay >= 0) CloseWorkDay(rec, day - rec.workDay > 1);
                rec.workDay = day;
                rec.workKinds.Clear();
                rec.workCounts.Clear();
            }
            if (!p.Spawned || !p.Awake()) return;
            var wt = p.CurJob?.workGiverDef?.workType;
            if (wt == null || NotRoutine.Contains(wt.defName)) return;
            int i = rec.workKinds.IndexOf(wt.defName);
            if (i < 0)
            {
                rec.workKinds.Add(wt.defName);
                rec.workCounts.Add(1);
            }
            else rec.workCounts[i]++;
        }

        /// <summary>A day is over: was it the same work again? A day away (or one never seen) clears the run.</summary>
        private static void CloseWorkDay(PawnPressure rec, bool missedDays)
        {
            if (missedDays)
            {
                rec.streakDays = 0;
                rec.streakWork = null;
                return;
            }
            int total = 0, top = 0;
            string topKind = null;
            for (int i = 0; i < rec.workKinds.Count; i++)
            {
                total += rec.workCounts[i];
                if (rec.workCounts[i] > top)
                {
                    top = rec.workCounts[i];
                    topKind = rec.workKinds[i];
                }
            }
            bool same = PressureMath.SameWorkDay(top, total);
            rec.streakDays = PressureMath.NextStreak(rec.streakDays, same, same && rec.streakWork == topKind && rec.streakDays > 0);
            rec.streakWork = same ? topKind : null;
        }

        private static void RefreshWork(PawnPressure rec)
        {
            int stage = PressureMath.WorkStage(rec.streakDays);
            if (stage > 0)
            {
                var work = DefDatabase<WorkTypeDef>.GetNamedSilentFail(rec.streakWork ?? "");
                if (rec.pawn == null || Pressures.SparedSameWork(rec.pawn, work)) stage = 0;
                else stage = PressureMath.WorkStageWithPassion(stage, Pressures.PassionFor(rec.pawn, work));
            }
            rec.workStage = stage;
        }

        // ---------------- Missing someone ----------------

        private static ThoughtDef wantPartner;
        private static bool wantLooked;

        /// <summary>The game's own "want to sleep with my partner" covers partners apart at home or on the same map.</summary>
        private static bool GameMindsPartner(Pawn p)
        {
            if (!wantLooked)
            {
                wantLooked = true;
                wantPartner = DefDatabase<ThoughtDef>.GetNamedSilentFail("WantToSleepWithSpouseOrLover");
            }
            if (wantPartner == null) return false;
            try
            {
                return wantPartner.Worker.CurrentState(p).Active;
            }
            catch
            {
                return false;
            }
        }

        private static string PairKey(Pawn a, Pawn b) =>
            a.thingIDNumber < b.thingIDNumber ? a.ThingID + "|" + b.ThingID : b.ThingID + "|" + a.ThingID;

        /// <summary>Not together: on different maps, in different caravans, or one away while the other is home.</summary>
        public static bool Apart(Pawn a, Pawn b)
        {
            var am = a.MapHeld;
            var bm = b.MapHeld;
            if (am != null && bm != null) return am != bm;
            var ac = CaravanUtility.GetCaravan(a);
            var bc = CaravanUtility.GetCaravan(b);
            if (ac != null && bc != null) return ac != bc;
            // Both somewhere the game doesn't say (in flight, at an outpost): don't guess.
            if (am == null && bm == null && ac == null && bc == null) return false;
            return true;
        }

        private void Missing(CouncilGameComponent comp, List<Pawn> everyone, int now)
        {
            touchedPairs.Clear();
            var social = S.socialWeb ? comp?.social : null;
            for (int i = 0; i < everyone.Count; i++)
            {
                var p = everyone[i];
                if (!Pressures.Eligible(p)) continue;
                var rec = For(p, true);
                rec.missed = null;
                rec.missedStage = 0;
                rec.missedTicks = 0;
                rec.missedPartner = false;
                var rels = p.relations?.DirectRelations;
                if (rels != null)
                {
                    bool gameMinds = false, gameAsked = false;
                    for (int r = 0; r < rels.Count; r++)
                    {
                        var rel = rels[r];
                        if (rel?.def == null || !LovePartnerRelationUtility.IsLovePartnerRelation(rel.def)) continue;
                        if (!gameAsked)
                        {
                            gameAsked = true;
                            gameMinds = GameMindsPartner(p);
                        }
                        if (!gameMinds) Consider(rec, rel.otherPawn, true, now);
                    }
                }
                if (Pressures.SparedMissingFriends(p)) continue;
                if (social != null)
                {
                    foreach (var t in social.ties)
                        if (t.kind == TieKind.Friends && t.announced && t.strength >= 40f && t.Involves(p))
                            Consider(rec, t.Other(p), false, now);
                }
                else if (p.relations != null)
                {
                    for (int j = 0; j < everyone.Count; j++)
                    {
                        var o = everyone[j];
                        if (o != p && o.relations != null && p.relations.OpinionOf(o) >= 60 && o.relations.OpinionOf(p) >= 60) Consider(rec, o, false, now);
                    }
                }
            }
            if (apartSince.Count > touchedPairs.Count)
                foreach (var k in apartSince.Keys.Where(k => !touchedPairs.Contains(k)).ToList())
                    apartSince.Remove(k);
        }

        private void Consider(PawnPressure rec, Pawn o, bool partner, int now)
        {
            var p = rec.pawn;
            if (o == null || o == p || !People.StillOurs(o) || !People.HasInnerLife(o) || !Apart(p, o)) return;
            string key = PairKey(p, o);
            touchedPairs.Add(key);
            if (!apartSince.TryGetValue(key, out int since) || since > now) apartSince[key] = since = now;
            int ticks = now - since;
            int stage = PressureMath.MissingStage(ticks / (float)GenDate.TicksPerDay, partner);
            if (stage > rec.missedStage || (stage > 0 && stage == rec.missedStage && ticks > rec.missedTicks))
            {
                rec.missed = o;
                rec.missedStage = stage;
                rec.missedTicks = ticks;
                rec.missedPartner = partner;
            }
        }

        // ---------------- Crowded rooms ----------------

        private class Crowd
        {
            public Room room;
            public bool dining;
            /// <summary>A party, ritual, wedding or council meeting is on in the room: being packed in is the point.</summary>
            public bool gathering;
            public int people, diners;
            public readonly List<Pawn> ours = new List<Pawn>();
        }

        private readonly Dictionary<Room, Crowd> crowds = new Dictionary<Room, Crowd>();

        private void Crowding(Map map, int now)
        {
            var diningRole = RoomUtil.DiningRoom;
            var recRole = RoomUtil.RecRoom;
            if (diningRole == null && recRole == null) return;
            crowds.Clear();
            var all = map.mapPawns.AllHumanlikeSpawned;
            for (int i = 0; i < all.Count; i++)
            {
                var x = all[i];
                if (x == null || x.Dead || x.Downed) continue;
                var room = x.GetRoom();
                if (room == null || !room.ProperRoom || room.PsychologicallyOutdoors) continue;
                var role = room.Role;
                if (role == null || (role != diningRole && role != recRole)) continue;
                if (!crowds.TryGetValue(room, out var c)) crowds[room] = c = new Crowd { room = room, dining = role == diningRole };
                c.people++;
                if (x.CurJobDef == JobDefOf.Ingest) c.diners++;
                if (x.GetLord()?.LordJob is LordJob_Joinable_Gathering) c.gathering = true;
                if (Pressures.Eligible(x)) c.ours.Add(x);
            }
            foreach (var c in crowds.Values)
            {
                if (c.gathering || c.people < PressureMath.CrowdMinPeople || c.ours.Count == 0) continue;
                int seats = c.dining ? Seats(c.room) : -1;
                int cells = c.room.CellCount;
                int level = PressureMath.CrowdLevel(c.people, cells, c.diners, seats);
                if (level <= 0) continue;
                foreach (var p in c.ours)
                {
                    if (p.DevelopmentalStage.Juvenile()) continue;
                    int l = Pressures.HatesCrowds(p) ? Math.Min(2, level + 1) : level;
                    var rec = For(p, true);
                    rec.crowdLevel = CrowdedLately(rec) ? Math.Max(rec.crowdLevel, l) : l;
                    rec.crowdTick = now;
                    rec.crowdRoom = c.room.Role?.label;
                    rec.crowdPeople = c.people;
                    rec.crowdSeats = c.dining ? seats : -1;
                    rec.crowdCells = cells;
                }
            }
            crowds.Clear();
        }

        /// <summary>Places to sit in the room: chairs, stools, benches (a bench of three seats three).</summary>
        private static int Seats(Room room)
        {
            int n = 0;
            var things = room.ContainedAndAdjacentThings;
            for (int i = 0; i < things.Count; i++)
            {
                var t = things[i];
                if (t?.def?.building == null || !t.def.building.isSittable || t.GetRoom() != room) continue;
                n += Math.Max(1, t.def.size.x * t.def.size.z);
            }
            return n;
        }

        // ---------------- Half-built work ----------------

        private void Construction(Map map, int now)
        {
            var home = map.areaManager?.Home;
            var u = new Unfinished();
            unfinished[map.uniqueID] = u;
            if (home == null) return;
            var labels = new Dictionary<string, int>();
            int frameStale = (int)(PressureMath.FrameStaleDays * GenDate.TicksPerDay);
            foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingFrame))
            {
                if (!(t is Frame f) || f.Faction != Faction.OfPlayer || !f.Spawned || !home[f.Position]) continue;
                int id = f.thingIDNumber;
                seenThings.Add(id);
                if (!lastProgress.TryGetValue(id, out int since) || !lastWork.TryGetValue(id, out float work) || f.workDone > work + 1f || since > now)
                {
                    lastProgress[id] = now;
                    lastWork[id] = f.workDone;
                    continue;
                }
                if (now - since < frameStale) continue;
                u.frames++;
                Count(labels, f.def.entityDefToBuild?.label, 5);
            }
            u.stage = PressureMath.UnfinishedStage(PressureMath.UnfinishedScore(u.frames));
            u.thing = labels.Count == 0 ? null : labels.OrderByDescending(kv => kv.Value).First().Key;
        }

        private static void Count(Dictionary<string, int> labels, string label, int weight)
        {
            if (label.NullOrEmpty()) return;
            labels.TryGetValue(label, out int n);
            labels[label] = n + weight;
        }

        /// <summary>Forgets frames and plans that are gone (built, cancelled, or outside the home area now).</summary>
        private void PruneConstruction()
        {
            foreach (var dict in new IDictionary<int, int>[] { firstSeen, lastProgress })
                foreach (var k in dict.Keys.Where(k => !seenThings.Contains(k)).ToList())
                    dict.Remove(k);
            foreach (var k in lastWork.Keys.Where(k => !seenThings.Contains(k)).ToList()) lastWork.Remove(k);
            seenThings.Clear();
            var maps = new HashSet<int>(Find.Maps.Where(m => m.IsPlayerHome).Select(m => m.uniqueID));
            foreach (var k in unfinished.Keys.Where(k => !maps.Contains(k)).ToList()) unfinished.Remove(k);
        }

        // ---------------- Saving ----------------

        public void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                people.RemoveAll(r => r?.pawn == null || !People.StillOurs(r.pawn));
                CouncilGameComponent.PruneByPawn(apartSince);
            }
            Scribe_Collections.Look(ref people, "people", LookMode.Deep);
            Scribe_Collections.Look(ref apartSince, "apartSince", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref firstSeen, "firstSeen", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref lastProgress, "lastProgress", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref lastWork, "lastWork", LookMode.Value, LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (people == null) people = new List<PawnPressure>();
                people.RemoveAll(r => r?.pawn == null);
                if (apartSince == null) apartSince = new Dictionary<string, int>();
                if (firstSeen == null) firstSeen = new Dictionary<int, int>();
                if (lastProgress == null) lastProgress = new Dictionary<int, int>();
                if (lastWork == null) lastWork = new Dictionary<int, float>();
                byPawn.Clear();
                unfinished.Clear();
                optionsDay = -1;
            }
        }
    }
}
