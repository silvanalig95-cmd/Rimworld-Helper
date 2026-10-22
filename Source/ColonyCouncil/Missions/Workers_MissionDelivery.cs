using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // Missions told from data: a neighbor asks for goods by a date, and the list (and the story around it) is in the
    // offer's MissionDelivery. The goods come from the colony's stockpiles when the neighbor's carts arrive: everything
    // (well paid), at least half of everything (its plain worth), or not enough (goodwill lost). Missions made this
    // way: rebuilding after a fire, a storm or a flood, and provisions for a hard season
    // (Defs/MissionDefs/Council_Missions_Delivery.xml).

    /// <summary>What a delivery mission asks for, in its variants, and on what terms.</summary>
    public class MissionDelivery : DefModExtension
    {
        /// <summary>The kind of mission (a text tag, and how missions of a kind rest between offers).</summary>
        public string kind;
        public float restDays = 45f;
        /// <summary>Stars in the Quests tab.</summary>
        public int rating = 2;
        /// <summary>How long the colony has, once it agrees.</summary>
        public FloatRange days = new FloatRange(8f, 11f);
        /// <summary>The full price, as a share of what the goods are worth.</summary>
        public float rewardFactor = 1.6f;
        public int goodwill = 12;
        public int penalty = 8;
        /// <summary>Someone of the neighbors the texts name ({DEAD}): the reeve who writes, the healer who asks. Null: nobody.</summary>
        public FloatRange person = FloatRange.Zero;
        public List<MissionVariant> variants = new List<MissionVariant>();

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            if (kind.NullOrEmpty()) yield return "delivery mission has no kind";
            if (variants.NullOrEmpty()) yield return "delivery mission has no variants";
        }
    }

    /// <summary>One way the mission can come: a tag for its texts, when it fits, and the goods it asks for.</summary>
    public class MissionVariant
    {
        public string tag;
        public float weight = 1f;
        public List<Season> seasons;
        /// <summary>The colony's era, as a tag expression.</summary>
        public string era;
        public List<MissionGoalSpec> goals = new List<MissionGoalSpec>();

        public bool Fits(Map map) =>
            (seasons.NullOrEmpty() || map != null && seasons.Contains(GenLocalDate.Season(map))) && HappeningWorker_Setting.EraFits(era);
    }

    /// <summary>
    /// One kind of goods asked for: a goal kind (meals, art, blocks, textiles, rawfood, medicine) or a thing's defName,
    /// a count before the colony's wealth is counted, and when it can be asked for at all (a research project done, an
    /// era). When it can't, <see cref="instead"/> is asked for (or nothing).
    /// </summary>
    public class MissionGoalSpec
    {
        public string what;
        public FloatRange count = new FloatRange(20f, 40f);
        public string research;
        public string era;
        public string instead;
        public FloatRange insteadCount = new FloatRange(20f, 40f);
    }

    /// <summary>A delivery mission told from its <see cref="MissionDelivery"/>.</summary>
    public class HappeningWorker_MissionDelivery : HappeningWorker_Mission
    {
        private MissionDelivery Delivery => def.GetModExtension<MissionDelivery>();

        protected override string Kind => Delivery?.kind ?? def.defName;

        protected override float RestDays => Delivery?.restDays ?? 45f;

        protected override int Rating => Delivery?.rating ?? 2;

        protected override bool Possible(Map map) => Delivery != null && Neighbor(map) != null && Delivery.variants.Any(v => v.Fits(map));

        private static int Scaled(Map map, FloatRange range) =>
            VarietyUtil.Round(Mathf.Max(1, Mathf.RoundToInt(range.RandomInRange * LivingEvents.WealthFactor(map))));

        /// <summary>Whether the colony could be asked for this at all: a known goal kind or a thing that exists, its era and research.</summary>
        private static bool CanAsk(Map map, string what, string research, string era)
        {
            if (what.NullOrEmpty() || !HappeningWorker_Setting.EraFits(era)) return false;
            switch (what)
            {
                case "meals":
                case "textiles":
                case "rawfood":
                case "medicine":
                    break;
                case "blocks":
                    if (!Avail.ResearchDone("Stonecutting") && Missions.Count(map, "blocks") < 20) return false;
                    break;
                case "art":
                    if (!CanSculpt(map)) return false;
                    break;
                default:
                    var d = DefDatabase<ThingDef>.GetNamedSilentFail(what);
                    if (d == null) return false;
                    if (!research.NullOrEmpty() && !Avail.ResearchDone(research) && VarietyUtil.InStores(map, d) < 10) return false;
                    return true;
            }
            return research.NullOrEmpty() || Avail.ResearchDone(research);
        }

        private static void Add(Mission m, string what, int n)
        {
            if (n <= 0) return;
            var g = m.goals.FirstOrDefault(x => x.what == what);
            if (g != null) g.count = VarietyUtil.Round(g.count + n);
            else m.goals.Add(new MissionGoal { what = what, count = n });
        }

        protected override bool Setup(Mission m, HappeningContext ctx, HappeningExtension ext)
        {
            var d = Delivery;
            var map = ctx.map;
            if (d == null) return false;
            var variant = d.variants.Where(v => v.Fits(map)).RandomElementByWeightWithFallback(v => Mathf.Max(0.01f, v.weight));
            if (variant == null) return false;
            m.faction = Neighbor(map);
            if (m.faction == null) return false;
            m.place = LivingEvents.TownOf(m.faction, map) ?? m.faction.Name;
            // The reeve deals with neighbours who need goods, when there is one (Interplay/).
            m.a = Interplay.MissionHead(map) ?? VarietyUtil.Best(map, SkillDefOf.Social) ?? StorytellerEvents.Adults(map).RandomElementWithFallback();
            if (m.a == null) return false;
            m.variant = variant.tag;
            foreach (var g in variant.goals)
            {
                if (g == null) continue;
                if (CanAsk(map, g.what, g.research, g.era)) Add(m, g.what, Scaled(map, g.count));
                else if (!g.instead.NullOrEmpty() && CanAsk(map, g.instead, null, null)) Add(m, g.instead, Scaled(map, g.insteadCount));
            }
            if (m.goals.Count == 0) return false;
            if (d.person.max > 0f)
            {
                // Someone of theirs the texts name: made for the name, never met.
                var kind = Arrivals.PersonKind(m.faction);
                var p = kind == null ? null : Arrivals.Make(kind, m.faction, d.person, null, null);
                m.name = p?.LabelShort ?? "?";
                if (p != null) Arrivals.Discard(p);
            }
            float worth = m.goals.Sum(g => g.count * Missions.UnitValue(g.what));
            m.reward = VarietyUtil.Round(Mathf.Max(20, Mathf.RoundToInt(worth * d.rewardFactor)));
            m.goodwill = d.goodwill;
            m.penalty = d.penalty;
            m.days = d.days.RandomInRange;
            Fill(m, ctx);
            return true;
        }

        public override bool Start(Mission m, Map map)
        {
            foreach (var g in m.goals) g.have = Missions.Count(map, g.what);
            return true;
        }

        public override void Check(Mission m, Map map, int now)
        {
            foreach (var g in m.goals) g.have = Missions.Count(map, g.what);
            bool all = m.goals.All(g => g.have >= g.count);
            if (m.stage == 0 && all)
            {
                // Everything's ready early: their carts come within half a day (told once).
                m.stage = 1;
                if (m.due < 0)
                {
                    m.due = Mathf.Min(now + GenDate.TicksPerDay / 2, m.deadline);
                    string line = Line(More?.ready, m, map, null, TextUtil.DaysString(m.due - now));
                    // At once, not in the evening's news: it says when the carts come, and they may have come by then.
                    if (!line.NullOrEmpty()) Messages.Message(line, MessageTypeDefOf.PositiveEvent, false);
                }
            }
            else if (m.stage == 1 && !all) m.stage = 0;
            if (m.stage == 1 && now >= m.due) Deliver(m, map);
            else if (now >= m.deadline) Deliver(m, map);
        }

        /// <summary>Their carts come: everything (well paid), at least half of everything (plain worth), or not enough.</summary>
        private void Deliver(Mission m, Map map)
        {
            foreach (var g in m.goals) g.have = Missions.Count(map, g.what);
            var spot = DropCellFinder.TradeDropSpot(map);
            var look = new LookTargets(new TargetInfo(spot, map));
            if (m.goals.All(g => g.have >= g.count))
            {
                // Their carts come for it and pay on the spot (Couriers/); else it's taken and paid at once.
                if (!Couriers.CollectGoals(map, m.goals.Select(g => new KeyValuePair<string, int>(g.what, g.count)).ToList(), m.faction, m.reward))
                {
                    foreach (var g in m.goals) Missions.Take(map, g.what, g.count);
                    VarietyUtil.PaySilver(map, spot, m.reward);
                }
                Missions.Goodwill(m, m.goodwill);
                Interplay.Notify_Delivered(m);
                Missions.Finish(m, MissionState.Done, "delivered", m.reward.ToString(), look);
                return;
            }
            if (m.goals.All(g => g.have * 2 >= g.count))
            {
                float worth = 0f;
                foreach (var g in m.goals) worth += Mathf.Min(g.count, g.have) * Missions.UnitValue(g.what);
                int pay = VarietyUtil.Round(Mathf.Max(10, Mathf.RoundToInt(worth)));
                // Their carts take what there is and pay for that (Couriers/); else it's taken and paid at once.
                if (!Couriers.CollectGoals(map, m.goals.Select(g => new KeyValuePair<string, int>(g.what, Mathf.Min(g.count, g.have))).ToList(), m.faction, pay))
                {
                    worth = 0f;
                    foreach (var g in m.goals) worth += Missions.Take(map, g.what, g.count) * Missions.UnitValue(g.what);
                    pay = VarietyUtil.Round(Mathf.Max(10, Mathf.RoundToInt(worth)));
                    VarietyUtil.PaySilver(map, spot, pay);
                }
                Missions.Finish(m, MissionState.Done, "partial", pay.ToString(), look, LetterDefOf.NeutralEvent);
                return;
            }
            Missions.Goodwill(m, -m.penalty);
            Missions.Finish(m, MissionState.Failed, "missed", null);
        }

        public override string Status(Mission m) =>
            m.stage == 1 ? "CC_Mission_Feast_Ready".Translate(m.faction?.Name ?? "?", TextUtil.DaysString(Mathf.Max(0, m.due - Find.TickManager.TicksGame))).ToString() : null;
    }
}
