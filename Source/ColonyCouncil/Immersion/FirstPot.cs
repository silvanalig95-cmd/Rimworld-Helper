using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// "Makes the first pot" (Defs/QuirkDefs/Rimfolk_Quirks_FirstPot.xml), a habit for someone up early: on a morning
    /// when there's coffee or tea in the colony's stores, they take a cup from the stockpile to a friend at their
    /// leisure, hand it over, and the friend drinks it there. One cup a trip; the habit comes round again a few hours
    /// later, so a morning holds one or two. Coffee and tea come only from mods (Vanilla Brewing Expanded and its Coffees
    /// and Teas, and others): found by name or by caffeine, so without such a mod the habit is never picked up. The cup is
    /// a real item from the stores, carried by the one who brings it; a friend whose drug policy, ideoligion or
    /// teetotalism rules it out is never offered one.
    /// </summary>
    public static class FirstPot
    {
        /// <summary>The morning hours it happens in (from, to: five to ten).</summary>
        public const int FromHour = 5, ToHour = 10;

        /// <summary>How far they'll carry a cup, and how near the stores must be.</summary>
        public const float Reach = 50f;

        private static readonly string[] Words = { "coffee", "tea", "latte", "espresso", "cappuccino", "chai", "matcha", "kombucha" };

        private static List<ThingDef> drinks;
        private static QuirkDef quirk;
        private static ThoughtDef cup;
        private static bool looked;
        private static int unlockedDay = -1;
        private static bool unlocked;

        private static void Look()
        {
            if (looked) return;
            looked = true;
            quirk = DefDatabase<QuirkDef>.GetNamedSilentFail("Rimfolk_Quirk_FirstPot");
            cup = DefDatabase<ThoughtDef>.GetNamedSilentFail("Rimfolk_Habit_FirstCup");
        }

        /// <summary>Every coffee and tea in the game (none without a mod that makes them).</summary>
        public static List<ThingDef> Drinks
        {
            get
            {
                if (drinks == null)
                {
                    drinks = new List<ThingDef>();
                    try
                    {
                        foreach (var d in DefDatabase<ThingDef>.AllDefsListForReading)
                            if (IsCoffeeOrTea(d))
                                drinks.Add(d);
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("first pot: drinks", e);
                    }
                }
                return drinks;
            }
        }

        /// <summary>
        /// A cup of coffee or tea: a drink (a drug or a fluid) named for one, or one with caffeine in it; never the raw
        /// leaves or beans, a plant, or a meal.
        /// </summary>
        public static bool IsCoffeeOrTea(ThingDef d)
        {
            var ing = d?.ingestible;
            if (ing == null || d.category != ThingCategory.Item || d.plant != null || ing.IsMeal) return false;
            if (ing.preferability == FoodPreferability.RawBad || ing.preferability == FoodPreferability.RawTasty) return false;
            if (!d.IsDrug && (ing.foodType & FoodTypeFlags.Fluid) == 0) return false;
            string chem = d.GetCompProperties<CompProperties_Drug>()?.chemical?.defName;
            if (chem != null && chem.IndexOf("caffeine", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return NamedForOne(d.label);
        }

        /// <summary>A label with one of the words for coffee or tea in it, as a whole word ("green tea", not "steak").</summary>
        public static bool NamedForOne(string label)
        {
            if (string.IsNullOrEmpty(label)) return false;
            foreach (var w in label.ToLowerInvariant().Split(' ', '-', ',', '(', ')', '\''))
                if (Array.IndexOf(Words, w) >= 0)
                    return true;
            return false;
        }

        /// <summary>
        /// Not a habit this colony can have yet: no coffee or tea in the game, or none it can make (an unlocked recipe)
        /// and none in its stores. Worked out once a day.
        /// </summary>
        public static bool Unfit(QuirkDef d)
        {
            Look();
            if (d == null || d != quirk) return false;
            if (Drinks.Count == 0) return true;
            int day = Find.TickManager.TicksGame / GenDate.TicksPerDay;
            if (day != unlockedDay)
            {
                unlockedDay = day;
                unlocked = Unlocked();
            }
            return !unlocked;
        }

        private static bool Unlocked()
        {
            try
            {
                foreach (var r in DefDatabase<RecipeDef>.AllDefsListForReading)
                {
                    ThingDef made;
                    try
                    {
                        made = r.ProducedThingDef;
                    }
                    catch
                    {
                        continue;
                    }
                    if (made != null && Drinks.Contains(made) && r.AvailableNow) return true;
                }
                foreach (var map in Find.Maps)
                    if (map.IsPlayerHome && InStores(map))
                        return true;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("first pot: unlocked", e);
            }
            return false;
        }

        private static bool InStores(Map map)
        {
            foreach (var d in Drinks)
                foreach (var t in map.listerThings.ThingsOfDef(d))
                    if (t.Spawned && t.IsInAnyStorage())
                        return true;
            return false;
        }

        /// <summary>Theirs to do now: they have the habit, it's morning, and there's coffee or tea in the stores.</summary>
        public static bool Fits(Pawn p)
        {
            Look();
            if (quirk == null || Drinks.Count == 0 || !MoreHabits.HasQuirk(p, quirk)) return false;
            int hour = GenLocalDate.HourOfDay(p.Map);
            return hour >= FromHour && hour < ToHour && InStores(p.Map);
        }

        /// <summary>The nearest cup in the stores they can reach and take, or null.</summary>
        public static Thing CupFor(Pawn p)
        {
            Thing best = null;
            float bestDist = float.MaxValue;
            foreach (var d in Drinks)
                foreach (var t in p.Map.listerThings.ThingsOfDef(d))
                {
                    if (!t.Spawned || !t.IsInAnyStorage() || t.IsForbidden(p) || !t.Position.InHorDistOf(p.Position, Reach)) continue;
                    float dist = t.Position.DistanceToSquared(p.Position);
                    if (dist >= bestDist || !p.CanReserveAndReach(t, PathEndMode.ClosestTouch, Danger.None)) continue;
                    best = t;
                    bestDist = dist;
                }
            return best;
        }

        /// <summary>Whom to take it to: a friend near them at their leisure, who'd drink it and hasn't had a cup from them today; the one they like best.</summary>
        public static Pawn FriendFor(Pawn p, ThingDef drink)
        {
            Look();
            Pawn best = null;
            int bestOpinion = int.MinValue;
            foreach (var o in p.Map.mapPawns.FreeColonistsSpawned)
            {
                if (o == p || !o.Position.InHorDistOf(p.Position, Reach) || !SmallHabits.CanListen(o) || HadOne(o, p) || !WillDrink(o, drink)) continue;
                int opinion = p.relations?.OpinionOf(o) ?? 0;
                if (opinion < 20 || opinion <= bestOpinion || !p.CanReach(o, PathEndMode.Touch, Danger.None)) continue;
                best = o;
                bestOpinion = opinion;
            }
            return best;
        }

        /// <summary>They had a cup from this one already (the memory of it lasts the morning).</summary>
        private static bool HadOne(Pawn whose, Pawn by)
        {
            var mem = whose.needs?.mood?.thoughts?.memories?.Memories;
            if (mem == null || cup == null) return false;
            foreach (var m in mem)
                if (m?.def == cup && m.otherPawn == by) return true;
            return false;
        }

        /// <summary>Someone who would drink it: it suits them, and for a drug their policy allows it for fun, they aren't teetotal, and their ideoligion lets them.</summary>
        public static bool WillDrink(Pawn x, ThingDef d)
        {
            if (x?.needs?.food == null || d == null || !x.RaceProps.CanEverEat(d)) return false;
            if (d.IsDrug)
            {
                if (x.IsTeetotaler()) return false;
                var policy = x.drugs?.CurrentPolicy;
                if (policy == null || !policy[d].allowedForJoy) return false;
                if (ModsConfig.IdeologyActive && !new HistoryEvent(HistoryEventDefOf.IngestedDrug, x.Named(HistoryEventArgsNames.Doer)).DoerWillingToDo()) return false;
            }
            return FoodUtility.WillEat(x, d, x);
        }

        /// <summary>
        /// Beside the friend with the cup: it's set down by them and they drink it there, glad of it (a small good memory
        /// about whoever brought it). False when it couldn't be given (the friend went off, or won't have it now).
        /// </summary>
        public static bool Hand(Pawn by, Pawn friend)
        {
            Look();
            var carried = by.carryTracker?.CarriedThing;
            // together: the job walked them to the friend's side; Together.Can checks they're face to face still.
            if (carried == null || friend == null || !Together.Can(by, friend) || !SmallHabits.CanListen(friend) || !WillDrink(friend, carried.def)) return false;
            if (!by.carryTracker.TryDropCarriedThing(friend.Position, ThingPlaceMode.Near, out var set) || set == null) return false;
            var drink = JobMaker.MakeJob(JobDefOf.Ingest, set);
            drink.count = 1;
            friend.jobs.StartJob(drink, JobCondition.InterruptForced, null, true, true);
            if (cup != null) CouncilGameComponent.GiveThought(friend, cup, by);
            return true;
        }
    }

    /// <summary>Makes the first pot: of a morning, a cup of coffee or tea from the stores to a friend at their leisure.</summary>
    public class JoyGiver_HabitFirstPot : JoyGiver_Habit
    {
        protected override HabitKind Kind => HabitKind.FirstPot;

        protected override bool Fits(Pawn p) => FirstPot.Fits(p);

        protected override Job Give(Pawn p)
        {
            var cup = FirstPot.CupFor(p);
            var friend = cup != null ? FirstPot.FriendFor(p, cup.def) : null;
            if (friend == null) return null;
            var job = JobMaker.MakeJob(def.jobDef, cup, friend);
            job.count = 1;
            return job;
        }
    }

    /// <summary>
    /// The cup carried: to the stores for one, then to the friend (following them a little if they move), set down by
    /// them and drunk there, with a word. Whatever is still in hand when it ends early is set down where they stand,
    /// for the haulers to take back. Ends at any danger and when the setting goes off, like the other habits.
    /// </summary>
    public class JobDriver_FirstPot : JobDriver
    {
        private string friendName;

        private Pawn Friend => job.targetB.Thing as Pawn;

        private HabitExtension Ext => job.def.GetModExtension<HabitExtension>();

        public override bool TryMakePreToilReservations(bool errorOnFailed) => pawn.Reserve(job.targetA, job, 1, 1, null, errorOnFailed);

        public override void Notify_Starting()
        {
            base.Notify_Starting();
            friendName = Friend?.LabelShort;
        }

        public override string GetReport()
        {
            if (!friendName.NullOrEmpty() && "CC_Habit_Report_FirstPot".CanTranslate())
                return "CC_Habit_Report_FirstPot".Translate(friendName.Named("OTHER")).Resolve();
            return base.GetReport();
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => !SmallHabits.On || pawn.Map == null || pawn.Map.dangerWatcher.DangerRating != StoryDanger.None);
            this.FailOnDespawnedOrNull(TargetIndex.B);
            this.FailOn(() => Friend == null || Friend.Dead || !Friend.Awake() || Friend.Drafted || Friend.InMentalState);
            AddFinishAction(condition =>
            {
                // A cup still in hand: set down here, for the haulers to put back.
                var held = pawn.carryTracker?.CarriedThing;
                if (held != null && FirstPot.Drinks.Contains(held.def)) pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out _);
            });
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch).FailOnDespawnedNullOrForbidden(TargetIndex.A);
            yield return Toils_Haul.StartCarryThing(TargetIndex.A, false, false, false);
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.Touch);
            var hand = ToilMaker.MakeToil("FirstPot");
            hand.initAction = Hand;
            hand.tickIntervalAction = Stay;
            hand.defaultCompleteMode = ToilCompleteMode.Delay;
            hand.defaultDuration = 300;
            hand.handlingFacing = true;
            yield return hand;
        }

        private void Hand()
        {
            try
            {
                var friend = Friend;
                SmallHabits.Done(pawn, HabitKind.FirstPot, Find.TickManager.TicksGame);
                if (!FirstPot.Hand(pawn, friend))
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }
                var ext = Ext;
                // Their quirk's moment, lived: the habit stays with them a while longer.
                if (ext?.quirk != null && MoreHabits.HasQuirk(pawn, ext.quirk)) CouncilGameComponent.Instance?.quirks?.Notify_Lived(pawn, ext.quirk);
                // together: said to the friend just handed the cup, a step away (Speech.Say shows it only when they're together).
                SmallHabits.Say(pawn, ext, friend);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("first pot hand over", e);
            }
        }

        private void Stay(int delta)
        {
            var friend = Friend;
            if (friend == null || !pawn.Position.InHorDistOf(friend.Position, 5f))
            {
                EndJobWith(JobCondition.Succeeded);
                return;
            }
            pawn.rotationTracker.FaceTarget(friend);
            if (pawn.needs?.joy != null) JoyUtility.JoyTickCheckEnd(pawn, delta, JoyTickFullJoyAction.None);
        }
    }
}
