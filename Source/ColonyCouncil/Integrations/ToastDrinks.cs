using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>
    /// A toast is raised with a drink from the colony's own stores (PlayOut's toasts, see PlayOutLord.cs): a social
    /// drink for those who may drink (beer, or Vanilla Brewing Expanded's wines, meads and ciders), or a soft one for
    /// those who may not (its tea and coffee, which a teetotaler can have). One each, at the start, only what's in stock
    /// and nobody else has claimed, only within their drug policy and their beliefs, never for someone with a chemical
    /// addiction. With nothing to pour, the toast goes on as before. Solemn words (a reading, a few words for the dead)
    /// pour nothing.
    /// </summary>
    public static class ToastDrinks
    {
        /// <summary>Only while the toast is starting: those who come late raise an empty hand.</summary>
        public const int PourTicks = 1500;

        /// <summary>Who has been poured a drink, by gathering (its lord's id). Not saved: a reload at most pours one more round.</summary>
        private static readonly Dictionary<int, HashSet<int>> poured = new Dictionary<int, HashSet<int>>();

        public static void Pour(Lord lord, Map map, IntVec3 spot, bool words)
        {
            if (words || lord == null || map == null || lord.ticksInToil > PourTicks) return;
            try
            {
                if (!poured.TryGetValue(lord.loadID, out var done))
                {
                    if (poured.Count > 20) poured.Clear();
                    done = new HashSet<int>();
                    poured[lord.loadID] = done;
                }
                List<Thing> drinks = null;
                foreach (var p in lord.ownedPawns.ToList())
                {
                    if (p == null || done.Contains(p.thingIDNumber) || !p.Spawned || p.Map != map || !p.Awake()) continue;
                    if (!PlayOuts.CanJoin(p) || p.CurJobDef == JobDefOf.Ingest) continue;
                    done.Add(p.thingIDNumber);
                    drinks ??= Drinks(map);
                    if (drinks.Count == 0) return;
                    var drink = Best(p, drinks, spot);
                    if (drink == null) continue;
                    var job = JobMaker.MakeJob(JobDefOf.Ingest, drink);
                    job.count = 1;
                    p.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("a drink for the toast", e);
            }
        }

        /// <summary>
        /// A drink to raise: a social drug that is alcohol (beer, wine, mead) or one a teetotaler may have (tea, coffee).
        /// Never smokeleaf, psychite tea or anything else social that isn't a drink of that kind.
        /// </summary>
        public static bool IsToastDrink(ThingDef d)
        {
            var ing = d?.ingestible;
            if (ing == null || !d.IsDrug || ing.drugCategory != DrugCategory.Social) return false;
            var drug = d.GetCompProperties<CompProperties_Drug>();
            return drug != null && (drug.teetotalerCanConsume || (drug.chemical != null && drug.chemical == ChemicalDefOf.Alcohol));
        }

        /// <summary>The social drinks in the colony's stores or lying about at home, not forbidden.</summary>
        private static List<Thing> Drinks(Map map)
        {
            var list = new List<Thing>();
            foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.Drug))
            {
                if (t == null || !t.Spawned || t.IsForbidden(Faction.OfPlayer) || !IsToastDrink(t.def)) continue;
                if (!t.IsInAnyStorage() && !map.areaManager.Home[t.Position]) continue;
                list.Add(t);
            }
            return list;
        }

        /// <summary>The finest drink this colonist may have, the nearest first among equals; null if none suits them.</summary>
        private static Thing Best(Pawn p, List<Thing> drinks, IntVec3 spot)
        {
            if (AddictionUtility.AddictedToAnything(p)) return null;
            // Teetotalers and the young get the soft drinks or nothing.
            bool teetotaler = (p.story?.traits != null && TraitDefOf.DrugDesire != null && p.story.traits.DegreeOfTrait(TraitDefOf.DrugDesire) < 0)
                              || p.DevelopmentalStage.Juvenile() || Flavor.HumanAge(p) < 16f;
            Thing best = null;
            float bestValue = -1f;
            float bestDist = float.MaxValue;
            foreach (var t in drinks)
            {
                if (t.Destroyed || !t.Spawned) continue;
                var drug = t.def.GetCompProperties<CompProperties_Drug>();
                if (teetotaler && (drug == null || !drug.teetotalerCanConsume)) continue;
                if (!Allowed(p, t.def)) continue;
                float value = t.def.BaseMarketValue;
                float dist = t.Position.DistanceToSquared(spot);
                if (value < bestValue || (value == bestValue && dist >= bestDist)) continue;
                if (t.IsForbidden(p) || !p.CanReserve(t, 10, 1) || !p.CanReach(t, PathEndMode.ClosestTouch, Danger.Some)) continue;
                best = t;
                bestValue = value;
                bestDist = dist;
            }
            return best;
        }

        /// <summary>Their drug policy lets them have it for joy, and their beliefs let them have it at all.</summary>
        private static bool Allowed(Pawn p, ThingDef d)
        {
            var policy = p.drugs?.CurrentPolicy;
            if (policy != null)
            {
                var entry = policy[d];
                if (entry != null && !entry.allowedForJoy) return false;
            }
            if (!ModsConfig.IdeologyActive) return true;
            if (!IdeoUtility.DoerWillingToDo(HistoryEventDefOf.IngestedDrug, p)) return false;
            return !d.IsNonMedicalDrug || IdeoUtility.DoerWillingToDo(HistoryEventDefOf.IngestedRecreationalDrug, p);
        }
    }
}
