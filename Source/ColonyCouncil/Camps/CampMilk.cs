using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    // The herd's milk for the colony to buy (the drovers' ask "milk"): only beasts of theirs that are on the map and have
    // milk to give now (the game's own CompMilkable, half full or more), and only as much as they give. A drover milks
    // them one after another where they graze, with the game's own milking, and carries the milk to the colony's gate
    // (CampCarry, help "sale"); once it's set down there, what it came to at its worth is carried over from the colony's
    // stores to their camp by a colonist (CampAnswers.Pay).
    public partial class Camp
    {
        /// <summary>The help key of the herd's milk the colony bought (CampCarry).</summary>
        public const string MilkSale = "sale";

        /// <summary>The beast's milk, if it has some to give now: the game's own, from a beast that gives it (grown, a cow where only cows give it), half full or more.</summary>
        private static CompMilkable MilkOf(Pawn a, ThingDef def = null)
        {
            var m = a?.TryGetComp<CompMilkable>();
            var props = m?.Props;
            if (props?.milkDef == null || (def != null && props.milkDef != def) || a.Faction == null || !Gives(a, m)) return null;
            return VisitorTradeMath.MilkGives(props.milkAmount, m.Fullness) > 0 ? m : null;
        }

        /// <summary>Their beasts on the map with milk to give now (never the dog at a drover's heels), the nearest to <paramref name="by"/> first.</summary>
        private IEnumerable<Pawn> MilkBeasts(ThingDef def, Pawn by = null) =>
            herd.Where(a => a != null && a.Spawned && !a.Dead && !a.Downed && a.Map == map && GroupOf(a)?.follow != true && MilkOf(a, def) != null
                            && (by == null || by.CanReserveAndReach(a, PathEndMode.Touch, Danger.Some)))
                .OrderBy(a => by == null ? 0 : a.Position.DistanceToSquared(by.Position));

        /// <summary>The herd's milk the colony could buy now: how much their beasts give together, of the kind most of it is, and how many beasts.</summary>
        public int MilkOffer(out ThingDef def, out int beasts)
        {
            def = null;
            beasts = 0;
            int best = 0;
            foreach (var g in MilkBeasts(null).GroupBy(a => MilkOf(a).Props.milkDef))
            {
                int n = g.Sum(a =>
                {
                    var m = MilkOf(a);
                    return VisitorTradeMath.MilkGives(m.Props.milkAmount, m.Fullness);
                });
                if (n <= best) continue;
                best = n;
                def = g.Key;
                beasts = g.Count();
            }
            return best;
        }

        /// <summary>What the herd's milk ready now comes to, at its worth.</summary>
        public int MilkCost() => VisitorTradeMath.MilkPrice(MilkOffer(out var def, out _), def?.BaseMarketValue ?? 0f);

        /// <summary>The herd's milk ready now as the ask names it ("about 26 milk for about 49 silver"), or that there's none.</summary>
        public string MilkWords()
        {
            int n = MilkOffer(out var def, out _);
            return n > 0 && def != null
                ? "CC_CampMilk_Offer".Translate(n, def.label, VisitorTradeMath.MilkPrice(n, def.BaseMarketValue)).Resolve()
                : "CC_CampMilk_NoneWord".Translate().Resolve();
        }

        /// <summary>The one of them who milks: the best with animals, free of another errand.</summary>
        private Pawn Milker() =>
            Free.Where(p => !p.WorkTypeIsDisabled(WorkTypeDefOf.Handling) && p != trader).OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Animals)?.Level ?? 0).FirstOrDefault();

        /// <summary>Why the herd's milk can't be bought now (null: it can).</summary>
        public string MilkSaleBlocked()
        {
            if (Pending(MilkSale) || Pending("milk")) return "CC_CampMilk_Going".Translate();
            if (MilkOffer(out _, out _) <= 0) return "CC_CampMilk_NoneReady".Translate();
            if (Milker() == null) return "CC_CampMilk_NoMilker".Translate();
            return CanBring ? null : "CC_CampErrand_No_gate".Translate().ToString();
        }

        /// <summary>The colony buys the herd's milk: a drover goes to milk their beasts that have some, to bring it to the colony's gate. False with none to milk or no one to go.</summary>
        public bool StartMilkSale()
        {
            int n = MilkOffer(out var def, out int beasts);
            var drover = Milker();
            if (n <= 0 || def == null || drover == null) return false;
            var gate = ErrandCell("gate", null, drover);
            if (!gate.IsValid) return false;
            carries.Add(new CampCarry { by = drover, help = MilkSale, def = def, want = beasts, to = gate, from = Now, until = Now + GenDate.TicksPerHour * 10 });
            return true;
        }

        /// <summary>The next beast to milk for the colony: one with milk of the kind sold, while not every beast it was asked for is done.</summary>
        private Job MilkSaleJob(Pawn p, CampCarry c)
        {
            if (c.stops >= c.want) return null;
            var beast = MilkBeasts(c.def, p).FirstOrDefault();
            return beast == null ? null : JobMaker.MakeJob(CampDefOf.Council_CampMilk, beast);
        }

        /// <summary>
        /// The colony's milk set down by its gate: what it came to at its worth goes over from the colony's stores to the
        /// camp, carried by a colonist. Should the colony have less silver by now, it pays what it has, and the drovers
        /// take it badly.
        /// </summary>
        private void MilkSold(Pawn p, CampCarry c, int n, Thing look)
        {
            int price = VisitorTradeMath.MilkPrice(n, c.def.BaseMarketValue);
            int pay = VisitorTradeMath.PayWith(price, CampAnswers.Silver(map));
            if (pay > 0) CampAnswers.Pay(this, pay);
            string key = pay >= price ? "CC_CampMilk_Sold" : pay > 0 ? "CC_CampMilk_SoldShort" : "CC_CampMilk_SoldUnpaid";
            if (pay < price) AddTension(pay > 0 ? 4f : 8f);
            string text = key.Translate(p.Named("X"), n.Named("COUNT"), c.def.label.Named("THING"), pay.Named("SILVER"), price.Named("PRICE"), Label.Named("NAME")).Resolve();
            Note(text, pay >= price ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.NeutralEvent, Importance.Notable, look, true);
        }
    }

    /// <summary>The colony buys the herd's milk (the drovers' ask): see <see cref="Camp.StartMilkSale"/>.</summary>
    public class DilemmaEffect_CampMilkSale : DilemmaEffect
    {
        public override string Unavailable(DilemmaContext ctx)
        {
            var c = CampOnMap.For(ctx);
            return c == null ? "CC_CampAsk_NotNow".Translate().ToString() : c.MilkSaleBlocked();
        }

        public override void Apply(DilemmaContext ctx)
        {
            var c = CampOnMap.For(ctx);
            if (c == null || (chance < 1f && !Rand.Chance(chance))) return;
            try
            {
                c.StartMilkSale();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("camp milk sale", e);
            }
        }
    }
}
