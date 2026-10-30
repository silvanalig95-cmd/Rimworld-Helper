using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The wardens' post as a trading stop: once the road wardens have settled as the colony's neighbours (Holding.cs), the
    /// carts that stop at their post take the colony's small loads on down the road (raw food, hides or wool, carried down
    /// to the post by one of the colony: <see cref="DilemmaEffect_RoadCarry"/>) for a fair road price, which a warden
    /// brings up to the gate when the carters come back through, days later. The wardens also bring word of what the
    /// carters saw on the road (the kind's <see cref="CampKindDef.roadWord"/> pool, about the world events running) when
    /// one of them comes up to the gate.
    /// </summary>
    public partial class Camp
    {
        /// <summary>The load on the road and the silver owed for it, due when the carters come back (RoadPostMath.DueDays).</summary>
        public int owedSilver, owedCount, owedTick = -1;
        /// <summary>How many loads have gone down the road with the carters, for the year's letter.</summary>
        public int roadLoads;
        public string owedWhat;
        public ThingDef owedDef;

        public bool LoadOnRoad => owedSilver > 0;

        /// <summary>Days until the carters come back with the silver, for the pages.</summary>
        public float LoadDaysLeft => !LoadOnRoad ? 0f : Mathf.Max(0f, (owedTick - Now) / (float)GenDate.TicksPerDay);

        /// <summary>A load handed to the wardens for the carters: what it is, how much, what it will fetch.</summary>
        public void SendDownRoad(string what, ThingDef def, int count, int price)
        {
            owedWhat = what;
            owedDef = def;
            owedCount = count;
            owedSilver = price;
            owedTick = Now + Rand.RangeInclusive(RoadPostMath.DueDaysMin, RoadPostMath.DueDaysMax) * GenDate.TicksPerDay;
            roadLoads++;
        }

        /// <summary>
        /// The carters have come back through: the load leaves the post with them, and the silver comes up to the colony in the
        /// hands of <paramref name="by"/> (a warden at the gate), or sent up the road when nobody of theirs could come.
        /// </summary>
        public void PayRoad(Pawn by)
        {
            int silver = owedSilver;
            if (silver <= 0 || map == null) return;
            string label = PlayCarry.Label(owedWhat, owedDef);
            TakeLoad();
            owedSilver = owedCount = 0;
            owedTick = -1;
            owedWhat = null;
            owedDef = null;
            try
            {
                var near = by != null && by.Spawned ? by.Position : FireCell;
                Couriers.PaySilver(map, near, silver, faction, by != null && by.Spawned ? by : null);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("road post pay", e);
            }
            Note(Text(by != null ? "CC_RoadPost_Paid" : "CC_RoadPost_PaidSent", by, null, silver, label), MessageTypeDefOf.PositiveEvent, Importance.Notable, by, true);
            AddTension(-2f);
        }

        /// <summary>The load goes with the carters: what the colony handed the wardens, out of their packs.</summary>
        private void TakeLoad()
        {
            int left = owedCount;
            foreach (var p in Here)
            {
                if (left <= 0) break;
                var inv = p.inventory;
                if (inv?.innerContainer == null) continue;
                foreach (var def in inv.innerContainer.Where(IsLoad).Select(t => t.def).Distinct().ToList())
                {
                    if (left <= 0) break;
                    int n = Mathf.Min(left, inv.innerContainer.TotalStackCountOfDef(def));
                    if (n <= 0) continue;
                    inv.RemoveCount(def, n);
                    left -= n;
                }
            }
        }

        private bool IsLoad(Thing t)
        {
            if (t?.def == null) return false;
            if (owedDef != null) return t.def == owedDef;
            return owedWhat == "rawfood" && RoadPost.IsRawFood(t.def);
        }

        /// <summary>
        /// Word from the road for their visit to the gate: a few lines from the kind's pool about the world events running
        /// (one event per line, by its kind: "ws:war"), or of a quiet road. Null when the kind brings none, or nothing fits.
        /// </summary>
        public List<string> RoadWord(Pawn speaker)
        {
            if (kind == null || kind.roadWord.NullOrEmpty() || speaker == null) return null;
            var running = WorldSituations.Instance?.Running.Where(s => s?.def != null).ToList() ?? new List<WorldSituation>();
            var lines = new List<string>();
            for (int i = 0; i < RoadPostMath.WordLines; i++)
            {
                var s = running.Count > 0 ? running.RandomElement() : null;
                string line = Flavor.FromPool(kind.roadWord, speaker, null, null, RoadPostMath.WordTags(s?.def.kind).ToArray());
                if (line.NullOrEmpty()) continue;
                line = line.Replace("{SITUATION}", CampWords.SituationName(s));
                if (!lines.Contains(line)) lines.Add(line);
            }
            return lines.Count > 0 ? lines : null;
        }

        /// <summary>For the pages: the load on the road, if any.</summary>
        public string LoadLine() =>
            !LoadOnRoad ? null
                : "CC_RoadPost_LoadLine".Translate(PlayCarry.Label(owedWhat, owedDef), owedSilver, TextUtil.DaysString(Mathf.Max(GenDate.TicksPerHour, owedTick - Now))).Resolve();

        private void ExposeRoadPost()
        {
            Scribe_Values.Look(ref owedSilver, "owedSilver");
            Scribe_Values.Look(ref owedCount, "owedCount");
            Scribe_Values.Look(ref owedTick, "owedTick", -1);
            Scribe_Values.Look(ref owedWhat, "owedWhat");
            Scribe_Defs.Look(ref owedDef, "owedDef");
            Scribe_Values.Look(ref roadLoads, "roadLoads");
        }
    }

    public static class RoadPost
    {
        /// <summary>Raw food by its kind alone (what the wardens hold of it is in their packs, not in storage).</summary>
        public static bool IsRawFood(ThingDef def)
        {
            var ing = def?.ingestible;
            if (ing == null || !def.IsNutritionGivingIngestible || def.IsCorpse || def.IsDrug || !ing.HumanEdible) return false;
            return ing.preferability == FoodPreferability.RawBad || ing.preferability == FoodPreferability.RawTasty;
        }

        /// <summary>The hide or wool the stores hold most of, when they hold at least <paramref name="count"/> of one kind; else null.</summary>
        public static ThingDef Hides(Map map, int count)
        {
            if (map == null) return null;
            return map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableAlways)
                .Where(t => t.Spawned && !t.IsForbidden(Faction.OfPlayer) && t.IsInAnyStorage() && t.def != null && (t.def.IsLeather || t.def.IsWool))
                .GroupBy(t => t.def)
                .Select(g => (def: g.Key, n: g.Sum(t => t.stackCount)))
                .Where(x => x.n >= count)
                .OrderByDescending(x => x.n)
                .Select(x => x.def)
                .FirstOrDefault();
        }
    }

    /// <summary>
    /// A load for the carters, carried down to the wardens' post by one of the colony (as <see cref="DilemmaEffect_CarryOut"/>
    /// to the camp): <see cref="DilemmaEffect_CarryOut.what"/> ("rawfood"), or, left empty, the hide or wool the stores hold
    /// most of. The price (RoadPostMath.Price, by the market value of what is taken first) is owed and comes up when the
    /// carters come back through (Camp.PayRoad). One load on the road at a time, and only once the wardens have settled.
    /// </summary>
    public class DilemmaEffect_RoadCarry : DilemmaEffect_CarryOut
    {
        public DilemmaEffect_RoadCarry()
        {
            to = PlaySpot.Camp;
            steward = false;
        }

        private int Amount(DilemmaContext ctx) =>
            PlayOutMath.CarryAmount(count, ctx?.count ?? 0, factor, steward ? CouncilGameComponent.Instance?.offices.CostFactor ?? 1f : 1f, tens);

        public override string Unavailable(DilemmaContext ctx)
        {
            var c = CampOnMap.For(ctx);
            if (c == null || !c.holding) return "CC_RoadPost_NotSettled".Translate().ToString();
            if (c.LoadOnRoad) return "CC_RoadPost_Busy".Translate().ToString();
            if (what.NullOrEmpty())
            {
                ctx.thing = RoadPost.Hides(ctx?.map ?? c.map, Amount(ctx));
                if (ctx.thing == null) return "CC_RoadPost_NoHides".Translate(Amount(ctx)).ToString();
            }
            return base.Unavailable(ctx);
        }

        public override void Apply(DilemmaContext ctx)
        {
            var c = CampOnMap.For(ctx);
            var map = ctx?.map ?? c?.map;
            if (c == null || !c.holding || c.LoadOnRoad || map == null) return;
            int n = Amount(ctx);
            if (what.NullOrEmpty() && ctx.thing == null) ctx.thing = RoadPost.Hides(map, n);
            var def = PlayCarry.DefOf(what, ctx.thing);
            if (what.NullOrEmpty() && def == null) return;
            if (ctx.map == null) ctx.map = map;
            // The price of what is taken first (the stores' plainest raw food, the hide there is most of).
            var first = PlayCarry.Stock(map, what, def).FirstOrDefault();
            float value = first?.def?.BaseMarketValue ?? def?.BaseMarketValue ?? 1f;
            c.SendDownRoad(what, def, n, RoadPostMath.Price(n, value));
            base.Apply(ctx);
        }
    }
}
