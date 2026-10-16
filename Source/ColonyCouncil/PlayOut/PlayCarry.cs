using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// What an errand takes from the stores (DilemmaEffect_CarryOut): the same things an answer's own taking would take
    /// (DilemmaEffect_Consume and the world events' medicine and silver), in the same order: the plainest medicine first,
    /// raw food in storage, silver and other things wherever they lie in the colony, never anything forbidden.
    /// </summary>
    public static class PlayCarry
    {
        /// <summary>The thing an errand's word names: silver, wood, a defName; the event's own thing when empty; none for a kind (raw food, medicine, meals).</summary>
        public static ThingDef DefOf(string what, ThingDef fallback)
        {
            switch (what)
            {
                case "silver": return ThingDefOf.Silver;
                case "wood": return ThingDefOf.WoodLog;
                case "rawfood":
                case "medicine":
                case "meals":
                    return null;
            }
            return what.NullOrEmpty() ? fallback : DefDatabase<ThingDef>.GetNamedSilentFail(what);
        }

        /// <summary>Raw food people can eat, sitting in storage: meat, vegetables, berries.</summary>
        public static bool IsRawFood(Thing t)
        {
            var ing = t?.def?.ingestible;
            if (ing == null || !t.def.IsNutritionGivingIngestible || t.def.IsCorpse || t.def.IsDrug || !ing.HumanEdible) return false;
            if (ing.preferability != FoodPreferability.RawBad && ing.preferability != FoodPreferability.RawTasty) return false;
            return t.Spawned && !t.IsForbidden(Faction.OfPlayer) && t.IsInAnyStorage();
        }

        /// <summary>Food few would thank you for (human meat, insect meat, or meals made with them): handed over last.</summary>
        private static int Unwelcome(Thing t)
        {
            try
            {
                if (FoodUtility.IsHumanlikeCorpseOrHumanlikeMeatOrIngredient(t)) return 2;
                if (FoodUtility.IsInsectCorpseOrInsectMeatIngredient(t)) return 1;
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("errand food", e);
            }
            return 0;
        }

        /// <summary>What there is to take, in the order it's taken.</summary>
        public static IEnumerable<Thing> Stock(Map map, string what, ThingDef def)
        {
            if (map == null) return Enumerable.Empty<Thing>();
            switch (what)
            {
                // Food carried out to someone else: what nobody would mind being given first, human and insect meat last.
                case "rawfood":
                    return map.listerThings.ThingsInGroup(ThingRequestGroup.FoodSourceNotPlantOrTree).Where(IsRawFood).OrderBy(Unwelcome);
                case "medicine": return DilemmaEffect_Consume.Medicine(map);
                case "meals": return PlaySpots.Meals(map).OrderBy(Unwelcome);
            }
            def = def ?? DefOf(what, null);
            if (def == null) return Enumerable.Empty<Thing>();
            return map.listerThings.ThingsOfDef(def).Where(t => t.Spawned && !t.IsForbidden(Faction.OfPlayer));
        }

        public static int Have(Map map, string what, ThingDef def) => Stock(map, what, def).Sum(t => t.stackCount);

        /// <summary>Takes so many from the stores (as many as there are, if fewer). How many were taken.</summary>
        public static int Take(Map map, string what, ThingDef def, int n)
        {
            if (map == null || n <= 0) return 0;
            int left = n;
            foreach (var t in Stock(map, what, def).ToList())
            {
                if (left <= 0) break;
                if (t == null || t.Destroyed || !t.Spawned) continue;
                int take = Mathf.Min(left, t.stackCount);
                t.SplitOff(take).Destroy();
                left -= take;
            }
            return n - left;
        }

        /// <summary>The stack the carrier picks up: the first in the order things are taken that they may and can get to.</summary>
        public static Thing StackFor(PlayOut p, Pawn carrier)
        {
            if (p?.map == null || carrier == null) return null;
            foreach (var t in Stock(p.map, p.what, p.thing))
            {
                if (t == null || !t.Spawned || t.IsForbidden(carrier)) continue;
                if (!carrier.CanReserve(t) || !carrier.CanReach(t, PathEndMode.ClosestTouch, Danger.Some)) continue;
                return t;
            }
            return null;
        }

        public static string Label(string what, ThingDef def)
        {
            switch (what)
            {
                case "rawfood": return "CC_Dilemma_RawFood".Translate();
                case "medicine": return "CC_Dilemma_Medicine".Translate();
                case "meals": return "CC_Play_Meals".Translate();
            }
            return (def ?? DefOf(what, null))?.label ?? what ?? "";
        }

        /// <summary>
        /// An errand that never went (or the setting off meanwhile): what it was to carry leaves the stores now, less
        /// what was already handed over.
        /// </summary>
        public static void Settle(PlayOut p)
        {
            if (p == null || p.kind != PlayKind.Carry || p.map == null || !Find.Maps.Contains(p.map)) return;
            int left = p.amount - p.tally;
            if (left > 0) Take(p.map, p.what, p.thing, left);
            p.tally = p.amount;
        }
    }

    /// <summary>The hunt's game and hunters: only safe game, only hunters the game itself would send.</summary>
    public static class PlayHunt
    {
        private static WorkGiverDef giver;
        private static bool looked;

        /// <summary>Wild game that won't turn on its hunters: no predator, nothing likely to go manhunter, nobody's animal.</summary>
        public static bool Safe(Pawn a, Map map) =>
            a != null && !a.Dead && a.Spawned && a.Map == map && !a.Downed && a.RaceProps != null && a.RaceProps.Animal && a.Faction == null
            && !a.RaceProps.predator && !a.InMentalState && !a.Position.Fogged(map) && PawnUtility.GetManhunterOnDamageChance(a) <= 0.1f;

        /// <summary>
        /// The game for a hunt: the event's own animal if it is one (and safe), else the kinds wanted first, else the best
        /// eating; never an animal marked for something else. With <paramref name="markedOnly"/>, only game already marked.
        /// </summary>
        public static List<Pawn> ChooseGame(Map map, Pawn x, string kinds, int n, bool markedOnly)
        {
            var result = new List<Pawn>();
            if (map == null) return result;
            var dm = map.designationManager;
            bool Fits(Pawn a) => Safe(a, map) && (markedOnly ? dm.DesignationOn(a, DesignationDefOf.Hunt) != null
                                                             : dm.DesignationOn(a) == null || dm.DesignationOn(a, DesignationDefOf.Hunt) != null);
            if (x != null && x.RaceProps != null && x.RaceProps.Animal)
            {
                if (Fits(x)) result.Add(x);
                return result;
            }
            var wanted = kinds.NullOrEmpty() ? null : kinds.Split('|').Select(k => k.Trim()).Where(k => k.Length > 0).ToList();
            result.AddRange(map.mapPawns.AllPawnsSpawned.ToList().Where(Fits)
                .OrderByDescending(a => wanted != null && wanted.Contains(a.def.defName))
                .ThenByDescending(a => a.GetStatValue(StatDefOf.MeatAmount))
                .Take(n));
            return result;
        }

        /// <summary>
        /// Someone the game would send hunting: able to, with a hunting weapon, no brawler; and nobody whose beliefs
        /// hold violence abhorrent or killing animals wrong (Wishes/Beliefs.cs).
        /// </summary>
        public static bool CanHunt(Pawn x) =>
            x?.workSettings != null && !x.WorkTypeIsDisabled(WorkTypeDefOf.Hunting) && WorkGiver_HunterHunt.HasHuntingWeapon(x)
            && !(x.story?.traits?.HasTrait(TraitDefOf.Brawler) ?? false)
            && !Beliefs.Forbids(x, Belief.NoHunting) && !Beliefs.Forbids(x, Belief.NoViolence);

        private static WorkGiverDef Giver
        {
            get
            {
                if (!looked)
                {
                    looked = true;
                    giver = DefDatabase<WorkGiverDef>.AllDefsListForReading.FirstOrDefault(w => w.giverClass == typeof(WorkGiver_HunterHunt));
                }
                return giver;
            }
        }

        /// <summary>The game's own hunting job for the nearest of the hunt's game this hunter may go after, or null.</summary>
        public static Job JobFor(PlayOut p, Pawn hunter)
        {
            var def = Giver;
            if (def == null || !(def.Worker is WorkGiver_Scanner sc) || p?.map == null) return null;
            if (sc.ShouldSkip(hunter, false)) return null;
            var dm = p.map.designationManager;
            foreach (var a in p.game.Where(a => Safe(a, p.map) && dm.DesignationOn(a, DesignationDefOf.Hunt) != null)
                         .OrderBy(a => a.Position.DistanceToSquared(hunter.Position)))
            {
                if (!sc.HasJobOnThing(hunter, a, false) || !hunter.CanReach(a, sc.PathEndMode, sc.MaxPathDanger(hunter))) continue;
                var job = sc.JobOnThing(hunter, a, false);
                if (job == null) continue;
                job.workGiverDef = def;
                return job;
            }
            return null;
        }

        /// <summary>Still on the hunt for this one's game.</summary>
        public static bool OnIt(PlayOut p, Pawn hunter)
        {
            var job = hunter?.CurJob;
            return job != null && job.def == JobDefOf.Hunt && job.targetA.Thing is Pawn a && p.game.Contains(a);
        }
    }
}
