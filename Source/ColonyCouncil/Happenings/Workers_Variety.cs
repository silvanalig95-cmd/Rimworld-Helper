using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // A third batch of world events, mostly small and everyday: neighbors who want something or have something to
    // offer, people passing through, kind weather for once, and things turned up in the ground. None ends in a fight.

    /// <summary>
    /// More of an event's text, kept in its def with the rest of it: a line under the letter (the event fills in
    /// {ITEM} and {DAYS}), and what's told when something comes of the answer later (an order collected, fortunes read).
    /// </summary>
    public class HappeningMoreText : DefModExtension
    {
        /// <summary>Under the letter's text: what's in the stores, what needs mending.</summary>
        [MustTranslate] public List<string> extra;
        /// <summary>Outcomes the event's code picks instead of the option's own (tagged by what happened).</summary>
        [MustTranslate] public List<string> outcomes;
        /// <summary>A message shortly before something comes due.</summary>
        [MustTranslate] public List<string> reminder;
        /// <summary>A line on the council's World page while it's pending ({DAYS}: how long until it's due).</summary>
        [MustTranslate] public List<string> coming;
        [MustTranslate] public List<string> doneTitles;
        [MustTranslate] public List<string> done;
        [MustTranslate] public List<string> failTitles;
        [MustTranslate] public List<string> fail;
        /// <summary>Called off: the other side is gone or at war with the colony.</summary>
        [MustTranslate] public List<string> cancelled;
    }

    /// <summary>What the events below share: their extra text, neighbors, and goods going in and out of the stores.</summary>
    public static class VarietyUtil
    {
        public static HappeningMoreText More(IncidentDef def) => def?.GetModExtension<HappeningMoreText>();

        /// <summary>
        /// A line that fits the colony's era, the season and the given tags, with {ITEM} and {DAYS} filled in first and
        /// then the event's own placeholders. Null when there's nothing to say.
        /// </summary>
        public static string Line(List<string> lines, HappeningContext ctx, IEnumerable<string> tags = null, string item = null, string days = null)
        {
            if (lines.NullOrEmpty() || ctx == null) return null;
            var context = new HashSet<string>();
            Flavor.AddEraTags(context);
            if (ctx.map != null)
            {
                switch (GenLocalDate.Season(ctx.map))
                {
                    case Season.Spring: context.Add("spring"); break;
                    case Season.Summer:
                    case Season.PermanentSummer: context.Add("summer"); break;
                    case Season.Fall: context.Add("fall"); break;
                    case Season.Winter:
                    case Season.PermanentWinter: context.Add("winter"); break;
                }
            }
            if (tags != null)
                foreach (var t in tags)
                    if (!t.NullOrEmpty()) context.Add(t);
            string s = Flavor.Pick(lines, context) ?? Flavor.PickText(lines);
            if (s.NullOrEmpty()) return null;
            if (item != null) s = s.Replace("{ITEM}", item);
            if (days != null) s = s.Replace("{DAYS}", days);
            return ctx.Format(s);
        }

        /// <summary>A neighbor on at least neutral terms, so nothing an event takes off their goodwill can turn them hostile.</summary>
        public static bool OnGoodTerms(Faction f) => f != null && f.HasGoodwill && f.PlayerGoodwill >= 0;

        /// <summary>The peoples <see cref="Arrivals.Friendly"/> would pick from, on good terms with the colony.</summary>
        public static IEnumerable<Faction> GoodNeighbors() =>
            Find.FactionManager.AllFactionsListForReading.Where(f => !f.IsPlayer && !f.Hidden && !f.defeated && !f.temporary && f.def.humanlikeFaction
                                                                     && f.def.basicMemberKind != null && !f.HostileTo(Faction.OfPlayer) && OnGoodTerms(f));

        /// <summary>The colony's best at a skill (adults who can do it at all), or null.</summary>
        public static Pawn Best(Map map, SkillDef skill) =>
            IncidentWorker_Happening.Colonists(map).Where(p => !p.DevelopmentalStage.Juvenile() && !p.Downed && !p.InMentalState && p.skills?.GetSkill(skill) is SkillRecord r && !r.TotallyDisabled)
                .OrderByDescending(p => p.skills.GetSkill(skill).Level).FirstOrDefault();

        private static IEnumerable<Thing> Stored(Map map, ThingDef def) =>
            map.listerThings.ThingsOfDef(def).Where(t => t.Spawned && !t.IsForbidden(Faction.OfPlayer) && t.IsInAnyStorage());

        /// <summary>How many of this sit in the colony's stockpiles and shelves (not forbidden).</summary>
        public static int InStores(Map map, ThingDef def) => map == null || def == null ? 0 : Stored(map, def).Sum(t => t.stackCount);

        /// <summary>Takes this many out of the stores. False (and nothing taken) when there aren't enough.</summary>
        public static bool TakeFromStores(Map map, ThingDef def, int n)
        {
            if (map == null || def == null || n <= 0) return false;
            var things = Stored(map, def).OrderBy(t => t.stackCount).ToList();
            if (things.Sum(t => t.stackCount) < n) return false;
            int left = n;
            foreach (var t in things)
            {
                if (left <= 0) break;
                int take = Mathf.Min(left, t.stackCount);
                t.SplitOff(take).Destroy();
                left -= take;
            }
            return true;
        }

        /// <summary>Silver left near a spot (or where traders drop their goods).</summary>
        public static void PaySilver(Map map, IntVec3 near, int amount)
        {
            if (map == null || amount <= 0) return;
            var cell = near.IsValid && near.InBounds(map) ? near : DropCellFinder.TradeDropSpot(map);
            while (amount > 0)
            {
                var silver = ThingMaker.MakeThing(ThingDefOf.Silver);
                silver.stackCount = Mathf.Min(amount, ThingDefOf.Silver.stackLimit);
                amount -= silver.stackCount;
                GenPlace.TryPlaceThing(silver, cell, map, ThingPlaceMode.Near);
            }
        }

        /// <summary>"wood x40", the way the game writes a number of something.</summary>
        public static string Bundle(ThingDef def, int n) => def == null ? "" : GenLabel.ThingLabel(def, null, Mathf.Max(1, n));

        /// <summary>A round number for an order: 35, 60, 120.</summary>
        public static int Round(int n) => n >= 100 ? Mathf.RoundToInt(n / 10f) * 10 : n >= 20 ? Mathf.RoundToInt(n / 5f) * 5 : n;

        /// <summary>Where someone stands, if they're at home on this map.</summary>
        public static IntVec3 Near(Pawn p, Map map) => p != null && p.Spawned && p.Map == map ? p.Position : IntVec3.Invalid;
    }

    // ===================== Stranded merchants =====================

    /// <summary>
    /// Merchants of a neighboring people broke down on the road past the colony. Send the best crafter to help (the
    /// better they are, the likelier the repair holds; paid in goods), sell them the materials, or let them manage.
    /// </summary>
    public class HappeningWorker_Stranded : HappeningWorker_Visitors
    {
        private static ThingDef Material => Avail.IsPreIndustrial ? ThingDefOf.WoodLog : ThingDefOf.Steel;

        protected override bool CanHappen(Map map, HappeningExtension ext) => base.CanHappen(map, ext) && VarietyUtil.Best(map, SkillDefOf.Crafting) != null;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = Arrivals.Friendly();
            ctx.a = VarietyUtil.Best(ctx.map, SkillDefOf.Crafting);
            if (ctx.faction == null || ctx.a == null) return false;
            MakeGroup(ctx, ext, Mathf.Max(1, ext.count.RandomInRange), ext.ages);
            ctx.thing = Material;
            ctx.variant = Avail.IsPreIndustrial ? 30 : 20;
            // They're stuck and they know it: well over the usual price.
            ctx.count = Mathf.Max(20, Mathf.RoundToInt(ctx.variant * ctx.thing.BaseMarketValue * 2.5f / 5f) * 5);
            ctx.tale = VarietyUtil.Bundle(ctx.thing, ctx.variant);
            return ctx.stranger != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(ctx.group.Prepend(ctx.stranger).ToList(), ctx.map, ctx.faction, null, ext.stayHours.RandomInRange) != null;

        public override string Unavailable(HappeningContext ctx, int option) =>
            option == 1 && ctx.thing != null && VarietyUtil.InStores(ctx.map, ctx.thing) < ctx.variant
                ? "CC_Dilemma_NotEnough".Translate(ctx.variant, ctx.thing.label).ToString()
                : null;

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            // Sold: the materials go, the silver comes.
            if (option == 1 && !byDefault && VarietyUtil.TakeFromStores(ctx.map, ctx.thing, ctx.variant))
                VarietyUtil.PaySilver(ctx.map, VarietyUtil.Near(ctx.a, ctx.map), ctx.count);
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    // ===================== A neighbor's order =====================

    /// <summary>
    /// A neighbor orders a good amount of something the colony keeps in its stores, at a good price, and sends for it
    /// in a few days. Ready in full, it's paid for and they think better of the colony; not ready, they leave with
    /// nothing and think a little worse of it. Kept as a memo ("order") until it comes due.
    /// </summary>
    public class HappeningWorker_Order : IncidentWorker_Happening
    {
        public const int DueDays = 5;
        private const float Premium = 1.5f;
        private const string Kind = "order";

        public static int Price(ThingDef def, int n) => Mathf.Max(20, Mathf.RoundToInt(n * def.BaseMarketValue * Premium / 5f) * 5);

        /// <summary>Plain goods a neighbor would buy by the stack: food, cloth, wood, medicine, metal. Never drugs, never silver.</summary>
        private static bool Orderable(ThingDef d, Faction f) =>
            d != null && d != ThingDefOf.Silver && d.category == ThingCategory.Item && TradeabilityUtility.PlayerCanSell(d.tradeability) && !d.IsDrug && !d.IsCorpse
            && d.stackLimit >= 10 && d.BaseMarketValue >= 0.5f && d.BaseMarketValue <= 40f
            && (f == null || (int)d.techLevel <= Mathf.Max((int)f.def.techLevel, (int)TechLevel.Medieval));

        private static IEnumerable<KeyValuePair<ThingDef, int>> Stock(Map map, Faction f) =>
            map.resourceCounter.AllCountedAmounts.Where(kv => kv.Value >= 10 && Orderable(kv.Key, f) && kv.Value * kv.Key.BaseMarketValue >= 40f).ToList();

        private static bool Pending => CouncilGameComponent.Instance?.happenings.Memos(Kind).Any() ?? false;

        protected override bool CanHappen(Map map, HappeningExtension ext) => !Pending && Arrivals.Friendly() != null && Stock(map, null).Any();

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            ctx.faction = Arrivals.Friendly();
            if (ctx.faction == null) return false;
            float target = Rand.Range(200f, 500f) * Mathf.Clamp(Mathf.Sqrt(map.wealthWatcher.WealthTotal / 40000f), 0.6f, 2f);
            var options = new List<(ThingDef def, int n)>();
            foreach (var kv in Stock(map, ctx.faction))
            {
                // A real order: at least ten, at most twice what the stores hold now.
                int n = VarietyUtil.Round(Mathf.Clamp(Mathf.RoundToInt(target / kv.Key.BaseMarketValue), 10, kv.Value * 2));
                float value = n * kv.Key.BaseMarketValue;
                if (value >= 80f && value <= 1500f) options.Add((kv.Key, n));
            }
            if (options.Count == 0) return false;
            var pick = options.RandomElement();
            ctx.thing = pick.def;
            ctx.variant = pick.n;
            ctx.count = Price(pick.def, pick.n);
            ctx.tale = VarietyUtil.Bundle(pick.def, pick.n);
            ctx.a = VarietyUtil.Best(map, SkillDefOf.Social) ?? Colonists(map).FirstOrDefault(p => !p.DevelopmentalStage.Juvenile());
            return ctx.a != null;
        }

        protected override string Extra(HappeningContext ctx) =>
            VarietyUtil.Line(VarietyUtil.More(def)?.extra, ctx, null, VarietyUtil.Bundle(ctx.thing, VarietyUtil.InStores(ctx.map, ctx.thing)));

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (option == 0 && ctx.thing != null && ctx.faction != null && ctx.variant > 0)
            {
                var m = CouncilGameComponent.Instance?.happenings.Remember(Kind, null, ctx.faction, ctx.thing.defName, null, false, ctx.a);
                if (m != null)
                {
                    m.amount = ctx.variant;
                    m.due = Find.TickManager.TicksGame + DueDays * GenDate.TicksPerDay;
                }
            }
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }

        /// <summary>Every hour: a reminder the day before, then the collection (memo: def = the goods, amount = how many, times = reminded).</summary>
        public static void Hourly(HappeningTracker t, CouncilGameComponent comp, int now)
        {
            var due = t.memos.Where(m => m.kind == Kind && !m.used).ToList();
            if (due.Count == 0) return;
            var incident = DefDatabase<IncidentDef>.GetNamedSilentFail("Council_Evt_BulkOrder");
            var more = VarietyUtil.More(incident);
            foreach (var m in due)
            {
                var goods = DefDatabase<ThingDef>.GetNamedSilentFail(m.def ?? "");
                var map = HappeningTracker.HomeMap();
                if (incident == null || goods == null || map == null || m.amount <= 0 || m.faction == null)
                {
                    t.Use(m);
                    continue;
                }
                var ctx = new HappeningContext
                {
                    incident = incident, map = map, a = m.other, faction = m.faction, thing = goods, variant = m.amount,
                    count = Price(goods, m.amount), tale = VarietyUtil.Bundle(goods, m.amount)
                };
                // Their people are gone, or at war with the colony now: nobody comes.
                if (m.faction.defeated || m.faction.HostileTo(Faction.OfPlayer))
                {
                    t.Use(m);
                    string off = VarietyUtil.Line(more?.cancelled, ctx);
                    if (!off.NullOrEmpty()) Messages.Message(off, MessageTypeDefOf.NeutralEvent, false);
                    continue;
                }
                if (now < m.due)
                {
                    if (m.times == 0 && now >= m.due - GenDate.TicksPerDay)
                    {
                        m.times = 1;
                        string soon = VarietyUtil.Line(more?.reminder, ctx);
                        if (!soon.NullOrEmpty()) Messages.Message(soon, MessageTypeDefOf.NeutralEvent, false);
                    }
                    continue;
                }
                t.Use(m);
                Collect(ctx, more, comp);
            }
        }

        /// <summary>The order on the World page ("what's coming"), or null.</summary>
        public static string WorldLine(Memo m, int now)
        {
            if (m == null || m.used || m.kind != Kind || m.faction == null || m.amount <= 0) return null;
            var incident = DefDatabase<IncidentDef>.GetNamedSilentFail("Council_Evt_BulkOrder");
            var goods = DefDatabase<ThingDef>.GetNamedSilentFail(m.def ?? "");
            if (incident == null || goods == null) return null;
            // Drawn every frame: the same line each time, no dice rolled.
            string line = Flavor.PickTip(VarietyUtil.More(incident)?.coming, m.tick);
            if (line.NullOrEmpty()) return null;
            var ctx = new HappeningContext { incident = incident, faction = m.faction, thing = goods, variant = m.amount, count = Price(goods, m.amount), tale = VarietyUtil.Bundle(goods, m.amount) };
            return ctx.Format(line.Replace("{DAYS}", TextUtil.DaysString(Mathf.Max(0, m.due - now))));
        }

        private static void Collect(HappeningContext ctx, HappeningMoreText more, CouncilGameComponent comp)
        {
            var map = ctx.map;
            var spot = DropCellFinder.TradeDropSpot(map);
            int have = VarietyUtil.InStores(map, ctx.thing);
            bool filled = have >= ctx.variant && VarietyUtil.TakeFromStores(map, ctx.thing, ctx.variant);
            var f = ctx.faction;
            if (filled)
            {
                VarietyUtil.PaySilver(map, spot, ctx.count);
                f.TryAffectGoodwillWith(Faction.OfPlayer, 6, true, false);
            }
            // A broken promise costs a little, but never a war.
            else if (f.PlayerGoodwill > -65) f.TryAffectGoodwillWith(Faction.OfPlayer, -5, true, false);
            // A filled order may make them regular buyers; a broken one ends that (Workers_Living.cs).
            LivingEvents.Notify_OrderCollected(f, ctx.thing, filled);
            string title = VarietyUtil.Line(filled ? more?.doneTitles : more?.failTitles, ctx) ?? ctx.incident.LabelCap.ToString();
            string text = VarietyUtil.Line(filled ? more?.done : more?.fail, ctx, new[] { have > 0 ? "some" : "none" }, VarietyUtil.Bundle(ctx.thing, have));
            if (text.NullOrEmpty()) return;
            Find.LetterStack.ReceiveLetter(title, text, filled ? LetterDefOf.PositiveEvent : LetterDefOf.NeutralEvent, new LookTargets(new TargetInfo(spot, map)));
            comp?.AddChronicle(new ChronicleEntry(ChronicleKind.World, title + ": " + text, ctx.a != null && !ctx.a.Dead ? ctx.a : null));
        }
    }

    // ===================== A traveling tinker =====================

    /// <summary>A tinker (a mender, after electricity) offers to mend every worn coat and battered weapon in the colony, for silver.</summary>
    public class HappeningWorker_Tinker : HappeningWorker_Visitors
    {
        private const int MaxItems = 30;

        private static bool Worn(Thing t) =>
            t != null && !t.Destroyed && t.def.useHitPoints && t.def.stackLimit == 1 && (t.def.IsApparel || t.def.IsWeapon) && t.MaxHitPoints > 0
            && t.HitPoints < t.MaxHitPoints * 0.9f;

        /// <summary>Worn or damaged clothes and weapons in the stores and on the colonists at home, worst first.</summary>
        private static List<Thing> Mendable(Map map)
        {
            var list = new List<Thing>();
            foreach (var group in new[] { ThingRequestGroup.Apparel, ThingRequestGroup.Weapon })
                foreach (var t in map.listerThings.ThingsInGroup(group))
                    if (t.Spawned && Worn(t) && !t.IsForbidden(Faction.OfPlayer) && t.IsInAnyStorage()) list.Add(t);
            foreach (var p in map.mapPawns.FreeColonistsSpawned)
            {
                if (p.apparel != null) list.AddRange(p.apparel.WornApparel.Where(Worn));
                if (p.equipment != null) list.AddRange(p.equipment.AllEquipmentListForReading.Where(Worn));
            }
            return list.Distinct().OrderBy(t => (float)t.HitPoints / t.MaxHitPoints).Take(MaxItems).ToList();
        }

        /// <summary>About a third of what the damage takes off their worth.</summary>
        private static int Cost(List<Thing> things)
        {
            float sum = 0f;
            foreach (var t in things)
            {
                float missing = 1f - (float)t.HitPoints / t.MaxHitPoints;
                sum += missing * t.GetStatValue(StatDefOf.MarketValueIgnoreHp) * 0.3f;
            }
            return Mathf.Clamp(Mathf.RoundToInt(sum / 5f) * 5, 25, 600);
        }

        protected override bool CanHappen(Map map, HappeningExtension ext) => base.CanHappen(map, ext) && Mendable(map).Count >= 4;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = Arrivals.Friendly();
            if (ctx.faction == null) return false;
            var things = Mendable(ctx.map);
            if (things.Count < 4) return false;
            MakeGroup(ctx, ext, 1, ext.ages);
            var r = ctx.stranger?.skills?.GetSkill(SkillDefOf.Crafting);
            if (r != null && !r.TotallyDisabled && r.Level < 8) r.Level = Rand.RangeInclusive(8, 14);
            ctx.variant = things.Count;
            ctx.count = Cost(things);
            // The worst of it, by name, for the letter.
            ctx.tale = TextUtil.JoinAnd(things.Select(t => GenLabel.ThingLabel(t.def, t.Stuff, 1)).Distinct().Take(4).ToList());
            ctx.a = VarietyUtil.Best(ctx.map, SkillDefOf.Crafting) ?? Colonists(ctx.map).FirstOrDefault(p => !p.DevelopmentalStage.Juvenile());
            return ctx.stranger != null && ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(new List<Pawn> { ctx.stranger }, ctx.map, ctx.faction, null, ext.stayHours.RandomInRange) != null;

        protected override string Extra(HappeningContext ctx) =>
            VarietyUtil.Line(VarietyUtil.More(def)?.extra, ctx, new[] { ctx.variant > 4 ? "many" : "few" }, ctx.tale);

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            // Paid: as many things as were paid for, worst first, as good as new.
            if (option == 0 && !byDefault && ctx.map != null)
                foreach (var t in Mendable(ctx.map).Take(Mathf.Max(1, ctx.variant)))
                    t.HitPoints = t.MaxHitPoints;
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    // ===================== Sickness at the neighbors =====================

    /// <summary>A neighbor's settlement is sick and out of medicine: give some, sell some, or keep it.</summary>
    public class HappeningWorker_NeighborsSick : IncidentWorker_Happening
    {
        private static ThingDef Medicine(Map map) =>
            map.resourceCounter.AllCountedAmounts.Where(kv => kv.Key.IsMedicine && kv.Value >= 6).OrderByDescending(kv => kv.Value).Select(kv => kv.Key).FirstOrDefault();

        protected override bool CanHappen(Map map, HappeningExtension ext) => Arrivals.Friendly() != null && Medicine(map) != null && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = Arrivals.Friendly();
            ctx.thing = Medicine(ctx.map);
            if (ctx.faction == null || ctx.thing == null) return false;
            int have = VarietyUtil.InStores(ctx.map, ctx.thing);
            if (have < 6) return false;
            ctx.variant = Mathf.Clamp(Mathf.RoundToInt(have * 0.4f), 3, 10);
            ctx.count = Mathf.Max(20, Mathf.RoundToInt(ctx.variant * ctx.thing.BaseMarketValue * 1.5f / 5f) * 5);
            ctx.tale = VarietyUtil.Bundle(ctx.thing, ctx.variant);
            ctx.a = VarietyUtil.Best(ctx.map, SkillDefOf.Medicine) ?? Colonists(ctx.map).FirstOrDefault(p => !p.DevelopmentalStage.Juvenile());
            return ctx.a != null;
        }

        protected override string Extra(HappeningContext ctx) =>
            VarietyUtil.Line(VarietyUtil.More(def)?.extra, ctx, null, VarietyUtil.Bundle(ctx.thing, VarietyUtil.InStores(ctx.map, ctx.thing)));

        /// <summary>How much medicine an answer sends: all of it, or half with the colony's healer's instructions (answer 3).</summary>
        private static int Sent(HappeningContext ctx, int option) => option == 3 ? Mathf.Max(1, ctx.variant / 2) : ctx.variant;

        public override string Unavailable(HappeningContext ctx, int option) =>
            (option <= 1 || option == 3) && ctx.thing != null && VarietyUtil.InStores(ctx.map, ctx.thing) < Sent(ctx, option)
                ? "CC_Dilemma_NotEnough".Translate(Sent(ctx, option), ctx.thing.label).ToString()
                : null;

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if ((option <= 1 || option == 3) && !byDefault)
            {
                bool sent = VarietyUtil.TakeFromStores(ctx.map, ctx.thing, Sent(ctx, option));
                if (sent && option == 1) VarietyUtil.PaySilver(ctx.map, VarietyUtil.Near(ctx.a, ctx.map), ctx.count);
            }
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    // ===================== A quarrel between neighbors =====================

    /// <summary>
    /// Two neighboring peoples quarrel over a spring, a herd, a strip of land or some salvage, and ask the colony to
    /// judge. Taking a side pleases one and annoys the other; the colony's best talker may find a middle way. Only
    /// peoples on good terms with the colony, so no answer can turn either of them hostile.
    /// </summary>
    public class HappeningWorker_Dispute : IncidentWorker_Happening
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            VarietyUtil.GoodNeighbors().Take(2).Count() == 2 && VarietyUtil.Best(map, SkillDefOf.Social) != null;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = Arrivals.Friendly(VarietyUtil.OnGoodTerms);
            ctx.enemy = ctx.faction == null ? null : Arrivals.Friendly(f => f != ctx.faction && VarietyUtil.OnGoodTerms(f));
            ctx.a = VarietyUtil.Best(ctx.map, SkillDefOf.Social);
            if (ctx.faction == null || ctx.enemy == null || ctx.enemy == ctx.faction || ctx.a == null) return false;
            // What it's about: 0 a spring, 1 a herd, 2 a strip of land, 3 salvage (not before electricity).
            ctx.variant = Rand.RangeInclusive(0, Avail.IsPreIndustrial ? 2 : 3);
            return true;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            switch (ctx.variant)
            {
                case 0: tags.Add("water"); break;
                case 1: tags.Add("herd"); break;
                case 2: tags.Add("land"); break;
                default: tags.Add("salvage"); break;
            }
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            // Both sides remember how it was judged (Workers_Living.cs).
            LivingEvents.Notify_Judged(ctx, option, risk, byDefault);
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    // ===================== Things turned up in the ground =====================

    /// <summary>Someone working outside turns up something old: a grave, a box of letters.</summary>
    public class HappeningWorker_Unearthed : IncidentWorker_Happening
    {
        protected static IEnumerable<Pawn> Outside(Map map) =>
            Colonists(map).Where(p => !p.DevelopmentalStage.Juvenile() && p.Awake() && !p.Drafted && !p.Downed && !p.Position.Roofed(map));

        protected override bool CanHappen(Map map, HappeningExtension ext) => Outside(map).Any();

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.a = Outside(ctx.map).RandomElementWithFallback();
            return ctx.a != null;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            var job = ctx.a?.CurJobDef;
            if (job == JobDefOf.Mine) tags.Add("mining");
            else if (job == JobDefOf.Sow || job == JobDefOf.Harvest || job == JobDefOf.CutPlant) tags.Add("farming");
        }
    }

    /// <summary>Old letters, from somewhere in a neighbor's lands: read them, send them on, or leave them be.</summary>
    public class HappeningWorker_OldLetters : HappeningWorker_Unearthed
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) => base.CanHappen(map, ext) && Arrivals.Friendly() != null;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            if (!base.Cast(ctx, ext)) return false;
            ctx.faction = Arrivals.Friendly();
            return ctx.faction != null;
        }
    }

    // ===================== Kind weather =====================

    /// <summary>Warm nights and soft rain: crops in the open fields jump a few days ahead.</summary>
    public class HappeningWorker_GrowingSpell : IncidentWorker_Happening
    {
        private static List<Plant> Behind(Map map) =>
            GameCondition_Hail.Crops(map).Where(p => p.Growth < 0.9f && p.def.plant != null && p.def.plant.growDays > 0.5f).ToList();

        protected override bool CanHappen(Map map, HappeningExtension ext)
        {
            var hail = DefDatabase<GameConditionDef>.GetNamedSilentFail("Council_Hail");
            return (hail == null || !map.gameConditionManager.ConditionIsActive(hail)) && Behind(map).Count >= 15 && Colonists(map).Count > 0;
        }

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var pawns = Colonists(ctx.map).Where(p => !p.DevelopmentalStage.Baby()).ToList();
            ctx.a = pawns.Where(p => p.skills?.GetSkill(SkillDefOf.Plants) is SkillRecord r && !r.TotallyDisabled && People.Passionate(r)).RandomElementWithFallback()
                    ?? VarietyUtil.Best(ctx.map, SkillDefOf.Plants) ?? pawns.RandomElementWithFallback();
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            float days = Rand.Range(2f, 4f);
            int n = 0;
            foreach (var p in Behind(ctx.map))
            {
                p.Growth = Mathf.Min(1f, p.Growth + days / p.def.plant.growDays);
                ctx.map.mapDrawer.MapMeshDirty(p.Position, MapMeshFlagDefOf.Things);
                if (ctx.place == null) ctx.place = p;
                n++;
            }
            ctx.count = n;
            return n > 0;
        }
    }

    /// <summary>A few days of warm weather out of season, in spring or fall: more growing days, fewer cold nights.</summary>
    public class GameCondition_WarmSpell : GameCondition
    {
        private const float Offset = 9f;

        public override float TemperatureOffset() => GameConditionUtility.LerpInOutValue(this, 12000f, Offset);

        // Slightly warmer daylight (colors only; see SkyTint).
        public override SkyTarget? SkyTarget(Map map) => SkyTint.Warm(this, map);

        public override float SkyTargetLerpFactor(Map map) => SkyTint.WarmLerp(this, map);
    }

    /// <summary>A warm spell, but not on top of another spell of odd weather (a cold snap, a heat wave).</summary>
    public class HappeningWorker_WarmSpell : HappeningWorker_Condition
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            base.CanHappen(map, ext) && !map.gameConditionManager.ActiveConditions.Any(c => Mathf.Abs(c.TemperatureOffset()) > 0.5f);
    }

    // ===================== Runaway lovers =====================

    /// <summary>
    /// Two young people of a neighboring people ran away together because their families won't allow it, and ask to
    /// stay. They wait out in the world until the colony answers, then walk in together (or go their way).
    /// </summary>
    public class HappeningWorker_Elopers : IncidentWorker_Happening
    {
        private static bool Suits(Faction f) => f.def.basicMemberKind?.RaceProps?.Humanlike == true;

        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            Arrivals.Friendly(Suits) != null && Colonists(map).Any(p => !p.DevelopmentalStage.Juvenile());

        private static bool Asexual(Pawn p) => p.story?.traits?.HasTrait(TraitDefOf.Asexual) ?? false;

        private static bool Likes(Pawn p, Gender other)
        {
            var t = p.story?.traits;
            if (t == null) return true;
            if (t.HasTrait(TraitDefOf.Asexual)) return false;
            if (t.HasTrait(TraitDefOf.Bisexual)) return true;
            return t.HasTrait(TraitDefOf.Gay) ? other == p.gender : other != p.gender;
        }

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = Arrivals.Friendly(Suits);
            ctx.a = Colonists(ctx.map).Where(p => !p.DevelopmentalStage.Juvenile())
                .RandomElementByWeightWithFallback(p => p.story?.traits?.HasTrait(TraitDefOf.Kind) == true ? 2f : 1f);
            if (ctx.faction == null || ctx.a == null) return false;
            var kind = ctx.faction.def.basicMemberKind;
            Pawn x = null;
            for (int i = 0; i < 3 && x == null; i++)
            {
                var p = Arrivals.Make(kind, ctx.faction, ext.ages);
                if (p == null) continue;
                if (Asexual(p)) Arrivals.Discard(p);
                else x = p;
            }
            if (x == null) return false;
            ctx.stranger = x;
            ctx.xWaiting = true;
            Arrivals.Hold(x);
            var traits = x.story?.traits;
            Gender g = traits != null && traits.HasTrait(TraitDefOf.Gay) ? x.gender
                : traits != null && traits.HasTrait(TraitDefOf.Bisexual) && Rand.Chance(0.3f) ? x.gender
                : x.gender == Gender.Male ? Gender.Female : Gender.Male;
            Pawn y = null;
            for (int i = 0; i < 4 && y == null; i++)
            {
                var p = Arrivals.Make(kind, ctx.faction, ext.ages, g);
                if (p == null) continue;
                if (Likes(p, x.gender)) y = p;
                else Arrivals.Discard(p);
            }
            if (y == null) return false;
            Arrivals.Hold(y);
            ctx.group.Add(y);
            x.relations?.AddDirectRelation(PawnRelationDefOf.Lover, y);
            ctx.tale = y.LabelShort;
            return true;
        }

        protected override void Abort(HappeningContext ctx)
        {
            foreach (var p in ctx.group.Prepend(ctx.stranger).Where(p => p != null && !p.Spawned).ToList()) Arrivals.Discard(p);
            ctx.stranger = null;
            ctx.group.Clear();
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            // Both came in: the second of them is as grateful as the first.
            var sheltered = DefDatabase<ThoughtDef>.GetNamedSilentFail("Council_Evt_Sheltered");
            if (option == 0 && sheltered != null)
            {
                using var _cause = Causes.Push(ctx.cause);
                foreach (var y in ctx.group.Where(p => p != null && !p.Dead && p.Faction == Faction.OfPlayer)) CouncilGameComponent.GiveThought(y, sheltered);
            }
            // Their families may come asking after them one day (Workers_Living.cs).
            if (option == 0) LivingEvents.Notify_Eloped(ctx);
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }

        /// <summary>Not taken in: both go back to the wide world (and home, or onward).</summary>
        public override void Cleanup(HappeningContext ctx)
        {
            foreach (var p in ctx.group.Prepend(ctx.stranger))
                if (p != null && !p.Spawned && p.Faction != Faction.OfPlayer) Arrivals.Release(p);
        }
    }

    // ===================== A family visit =====================

    /// <summary>
    /// A colonist's real relative, living with a neighboring people, comes to visit for a day: a good day for both,
    /// and the colony can ask them to stay.
    /// </summary>
    public class HappeningWorker_FamilyVisit : HappeningWorker_Visitors
    {
        private static bool CanVisit(Pawn r)
        {
            if (r == null || r.Dead || r.Discarded || r.Spawned || !r.RaceProps.Humanlike || r.DevelopmentalStage.Juvenile() || !Find.WorldPawns.Contains(r)) return false;
            var f = r.Faction;
            if (f == null || f.IsPlayer || f.Hidden || f.defeated || f.temporary || f.HostileTo(Faction.OfPlayer) || f.leader == r) return false;
            if (r.royalty != null && r.royalty.AllTitlesForReading.Count > 0) return false;
            if (r.IsPrisoner || People.Kidnapped(r) || r.GetCaravan() != null || QuestUtility.IsReservedByQuestOrQuestBeingGenerated(r)) return false;
            var comp = CouncilGameComponent.Instance;
            // A relative who may write after their last visit (a "kin" memo) can still come again; the new visit replaces it.
            if (comp != null && (comp.nemeses.IsNemesis(r) || comp.happenings.memos.Any(m => !m.used && m.pawn == r && m.kind != LivingEvents.Kin)
                                 || comp.happenings.departed.Any(d => d.pawn == r))) return false;
            return !Find.LetterStack.LettersListForReading.Any(l => l is ChoiceLetter_Happening h && !h.resolved && h.ctx?.stranger == r);
        }

        private static List<(Pawn a, Pawn r)> Pairs(Map map) =>
            Colonists(map).Where(a => !a.DevelopmentalStage.Baby() && a.relations != null)
                .SelectMany(a => a.relations.FamilyByBlood.Where(CanVisit).Select(r => (a, r))).ToList();

        protected override bool CanHappen(Map map, HappeningExtension ext) => Pairs(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var pick = Pairs(ctx.map).RandomElementWithFallback();
            if (pick.a == null || pick.r == null) return false;
            ctx.a = pick.a;
            ctx.stranger = pick.r;
            ctx.faction = pick.r.Faction;
            ctx.relation = PawnRelationUtility.GetMostImportantRelation(pick.a, pick.r)?.GetGenderSpecificLabel(pick.r);
            return !ctx.relation.NullOrEmpty();
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(new List<Pawn> { ctx.stranger }, ctx.map, ctx.faction, null, ext.stayHours.RandomInRange) != null;

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            // Gone home: they may write one day (Workers_Living.cs).
            LivingEvents.Notify_FamilyVisited(ctx);
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }

        /// <summary>They were someone before this event: never thrown away. Already on the map without a visit to make, they go home.</summary>
        protected override void Abort(HappeningContext ctx)
        {
            var x = ctx.stranger;
            if (x != null && x.Spawned) Arrivals.SendAway(new[] { x });
            ctx.stranger = null;
            ctx.group.Clear();
        }
    }

    // ===================== A fortune teller =====================

    /// <summary>
    /// A fortune teller reads every colonist's fortune for silver. Most are good, a few aren't; the cheerful hear good
    /// news more often, the gloomy and the nervous bad, and the very clever don't believe a word of it either way.
    /// </summary>
    public class HappeningWorker_FortuneTeller : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) => base.CanHappen(map, ext) && Colonists(map).Count >= 2;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = Arrivals.Friendly();
            if (ctx.faction == null) return false;
            MakeGroup(ctx, ext, 1, ext.ages);
            var pawns = Colonists(ctx.map).Where(p => !p.DevelopmentalStage.Baby()).ToList();
            ctx.count = Mathf.Clamp(VarietyUtil.Round(pawns.Count * (Avail.IsPreIndustrial ? 5 : 8)), 20, 150);
            ctx.a = pawns.Where(p => !p.DevelopmentalStage.Juvenile()).RandomElementWithFallback();
            return ctx.stranger != null && ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(new List<Pawn> { ctx.stranger }, ctx.map, ctx.faction, GatheringSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (option == 0 && !byDefault && ctx.map != null)
            {
                var unlucky = Tell(ctx);
                if (unlucky != null)
                {
                    ctx.b = unlucky;
                    outcome = VarietyUtil.Line(VarietyUtil.More(def)?.outcomes, ctx, new[] { "unlucky" }) ?? outcome;
                    // The bad fortune may seem to come true, harmlessly (Workers_Living.cs).
                    LivingEvents.Notify_BadFortune(unlucky);
                }
                var lines = Ext?.lines;
                if (!lines.NullOrEmpty()) Arrivals.Say(ctx.stranger, Flavor.PickText(lines));
            }
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }

        /// <summary>Everyone's fortune. Returns someone who got a bad one (for the telling), or null.</summary>
        private static Pawn Tell(HappeningContext ctx)
        {
            var good = DefDatabase<ThoughtDef>.GetNamedSilentFail("Council_Evt_GoodFortune");
            var bad = DefDatabase<ThoughtDef>.GetNamedSilentFail("Council_Evt_BadFortune");
            var tooSmart = DefDatabase<TraitDef>.GetNamedSilentFail("TooSmart");
            var naturalMood = DefDatabase<TraitDef>.GetNamedSilentFail("NaturalMood");
            var nerves = DefDatabase<TraitDef>.GetNamedSilentFail("Nerves");
            Pawn unlucky = null;
            using var _cause = Causes.Push(ctx.cause);
            foreach (var p in Colonists(ctx.map).Where(p => !p.DevelopmentalStage.Baby()))
            {
                p.needs?.joy?.GainJoy(0.12f, JoyKindDefOf.Social);
                var traits = p.story?.traits;
                if (tooSmart != null && traits != null && traits.HasTrait(tooSmart)) continue;
                float chance = 0.25f;
                if (traits != null)
                {
                    if (naturalMood != null && traits.HasTrait(naturalMood)) chance -= traits.DegreeOfTrait(naturalMood) * 0.1f;
                    if (nerves != null && traits.HasTrait(nerves) && traits.DegreeOfTrait(nerves) < 0) chance += 0.1f;
                }
                bool isBad = Rand.Chance(Mathf.Clamp(chance, 0.05f, 0.6f));
                CouncilGameComponent.GiveThought(p, isBad ? bad : good);
                if (isBad && (unlucky == null || Rand.Bool)) unlucky = p;
            }
            return unlucky;
        }
    }
}
