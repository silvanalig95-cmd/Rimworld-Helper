using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // The second round of visitors (docs/content-proposals-A.md 7 to 12): pilgrims to the colony's relic, pilgrims at
    // its anima tree, kin of a colonist's xenotype, gawkers at the landed gravship, a book swapper, and the regulars'
    // own lives. Each comes only when the colony has the thing (and the DLC it belongs to), walks in to the gate with
    // its letter (VisitorTrip, Visitors/), and does what the answer says on the map (VisitorsMore2.cs).

    /// <summary>
    /// Visitors who walk in to the gate with their letter, waiting there for the answer: only when they really can
    /// (the setting on, the map safe, room for another group), so the letter never speaks of people who aren't there.
    /// </summary>
    public abstract class HappeningWorker_AtGate2 : HappeningWorker_Visitors
    {
        /// <summary>What they are, for their lines (VisitorWords' tags).</summary>
        protected abstract string What { get; }

        public static bool CanCome(Map map) =>
            Visitors.On && map != null && map.IsPlayerHome && !MapFacts.InVacuum(map) && Visitors.Safe(map) && VisitorMath.Room(Visitors.OnMap(map))
            && (VisitorTracker.Instance?.trips.Count(t => t.state != VisitState.Done) ?? VisitorMath.MaxTrips) < VisitorMath.MaxTrips;

        protected override bool CanHappen(Map map, HappeningExtension ext) => CanCome(map) && base.CanHappen(map, ext);

        protected static List<Pawn> Grown(Map map) => Colonists(map).Where(p => !p.DevelopmentalStage.Juvenile() && !p.Downed).ToList();

        protected static Faction People(Func<Faction, float> factor = null) => Arrivals.Friendly(Visitors.Usable, factor);

        /// <summary>
        /// They walk in from the edge to the gate now (<paramref name="with"/> at their head, kept in the world till
        /// now), and wait there for the letter's answer: X is the first of them, the rest the event's group. Null when
        /// they can't come.
        /// </summary>
        protected VisitorTrip ComeToGate(HappeningContext ctx, int count, Pawn with = null, int children = 0, FloatRange? ages = null)
        {
            var tracker = VisitorTracker.Instance;
            if (tracker == null || ctx.map == null || !Visitors.Usable(ctx.faction)) return null;
            if (with != null && (!WorldSituationWorker.FreeInWorld(with) || with.Faction != ctx.faction)) return null;
            int now = Find.TickManager.TicksGame;
            var t = new VisitorTrip
            {
                kind = VisitKind.Wait, map = ctx.map, faction = ctx.faction, what = What, count = Math.Max(1, count), children = children, with = with,
                ages = ages ?? new FloatRange(18f, 50f), created = now, due = now, giveUp = now + VisitorMath.GiveUpTicks, spotKind = VisitSpot.Gate, hours = 3f,
                ctxRef = ctx
            };
            if (!tracker.ComeNow(t)) return null;
            var here = t.people.Where(p => p != null && p.Spawned).ToList();
            if (here.Count == 0 || with != null && !here.Contains(with))
            {
                tracker.SendHome(t);
                return null;
            }
            ctx.stranger = with ?? here[0];
            ctx.group = here.Where(p => p != ctx.stranger).ToList();
            return t;
        }
    }

    // ===================== Pilgrims to the relic (Ideology) =====================

    /// <summary>
    /// People of the faith of the colony's relic walk a long way to pray before it, and ask at the gate to be let in to
    /// the reliquary. Only with a relic in a reliquary. Their people hold that faith, or they are its converts among a
    /// people who don't; either way they are of it.
    /// </summary>
    public class HappeningWorker_RelicPilgrims : HappeningWorker_AtGate2
    {
        protected override string What => "pilgrims";

        public static Ideo IdeoOf(DilemmaContext ctx) => VisitorWork2.IdeoOf(VisitorWork2.RelicIn((ctx as HappeningContext)?.place));

        protected override bool CanHappen(Map map, HappeningExtension ext) => ModsConfig.IdeologyActive && VisitorWork2.Reliquaries(map).Count > 0 && base.CanHappen(map, ext);

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var reliquary = VisitorWork2.Reliquaries(ctx.map).RandomElementWithFallback();
            var relic = VisitorWork2.RelicIn(reliquary);
            var ideo = VisitorWork2.IdeoOf(relic);
            if (ideo == null) return false;
            ctx.place = reliquary;
            ctx.tale = relic.LabelShort;
            ctx.placeName = ideo.name;
            ctx.faction = People(Peoples.Weigh(ext.prefers, f => f.ideos != null && f.ideos.Has(ideo) ? 6f : 1f));
            if (ctx.faction == null) return false;
            var grown = Grown(ctx.map);
            ctx.a = grown.Where(p => p.Ideo == ideo).OrderByDescending(p => p.ideo?.Certainty ?? 0f).FirstOrDefault() ?? grown.RandomElementWithFallback();
            ctx.variant = ext.count.RandomInRange;
            ctx.count = ctx.variant * 25;
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            var ideo = IdeoOf(ctx);
            var t = ComeToGate(ctx, ctx.variant);
            if (t == null) return false;
            // Of the relic's faith, whatever their people's.
            foreach (var p in t.people)
                if (p?.ideo != null && ideo != null && p.Ideo != ideo)
                    p.ideo.SetIdeo(ideo);
            return true;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            var ideo = IdeoOf(ctx);
            tags.Add(ideo != null && ctx.faction?.ideos != null && ctx.faction.ideos.Has(ideo) ? "theirFaith" : "converts");
            if (ideo != null && ctx.a?.Ideo == ideo) tags.Add("believer");
        }
    }

    /// <summary>
    /// The relic's faithful in the colony and the rest: a memory for those of the relic's ideoligion (and their
    /// certainty steadied or shaken), another for those who aren't (strangers let into the reliquary, for one).
    /// </summary>
    public class DilemmaEffect_RelicFaith : DilemmaEffect
    {
        public ThoughtDef believers;
        public ThoughtDef others;
        public float certainty;

        private static List<Pawn> Of(DilemmaContext ctx, bool faithful)
        {
            var ideo = HappeningWorker_RelicPilgrims.IdeoOf(ctx);
            return ctx?.map == null || ideo == null ? new List<Pawn>() : ctx.Colonists.Where(p => People.HasInnerLife(p) && (p.Ideo == ideo) == faithful).ToList();
        }

        public override void Apply(DilemmaContext ctx)
        {
            // apart: how the colony's faithful feel about the answer; the pilgrims' prayer plays out on its own.
            foreach (var p in Of(ctx, true))
            {
                if (believers != null) CouncilGameComponent.GiveThought(p, believers);
                if (Mathf.Abs(certainty) > 0.0001f) p.ideo?.OffsetCertainty(certainty);
            }
            if (others != null)
                foreach (var p in Of(ctx, false))
                    CouncilGameComponent.GiveThought(p, others);
        }

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var faithful = Of(ctx, true);
            var rest = Of(ctx, false);
            string a = Words(faithful, true), b = Words(rest, false);
            if (believers != null) sheet.Thought(faithful, a, believers, p => null);
            if (faithful.Count > 0) DilemmaImpact.Certainty(sheet, a, certainty);
            if (others != null) sheet.Thought(rest, b, others, p => null);
        }

        private static string Words(List<Pawn> who, bool faithful)
        {
            if (who.Count == 0) return null;
            if (who.Count <= 3) return TextUtil.JoinAnd(who.Select(p => p.LabelShort).ToList());
            return (faithful ? "CC_Impact_V2Faithful" : "CC_Impact_V2NotFaithful").Translate(who.Count).Resolve();
        }
    }

    // ===================== Pilgrims at the anima tree (Royalty) =====================

    /// <summary>
    /// Psycasters and tribal folk ask leave to meditate at the colony's anima tree. Only with an anima tree on the map.
    /// Their meditation feeds the tree's grass as a colonist's does (JobDriver_VisitMeditate).
    /// </summary>
    public class HappeningWorker_AnimaPilgrims : HappeningWorker_AtGate2
    {
        protected override string What => "pilgrims";

        protected override bool CanHappen(Map map, HappeningExtension ext) => ModsConfig.RoyaltyActive && VisitorWork2.AnimaTree(map) != null && base.CanHappen(map, ext);

        private static bool Tribal(Faction f) => f?.def != null && f.def.techLevel <= TechLevel.Neolithic;

        private static bool Empire(Faction f) => f?.def?.defName == "Empire";

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var tree = VisitorWork2.AnimaTree(ctx.map);
            if (tree == null) return false;
            ctx.place = tree;
            ctx.faction = People(Peoples.Weigh(ext.prefers, f => Tribal(f) || Empire(f) ? 4f : 1f));
            if (ctx.faction == null) return false;
            var grown = Grown(ctx.map);
            ctx.a = grown.Where(p => p.HasPsylink).RandomElementWithFallback() ?? grown.RandomElementWithFallback();
            ctx.variant = ext.count.RandomInRange;
            ctx.count = ctx.variant * 20;
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) => ComeToGate(ctx, ctx.variant) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            if (ctx.a?.HasPsylink == true) tags.Add("psycaster");
            if (Tribal(ctx.faction)) tags.Add("tribal");
            if (Empire(ctx.faction)) tags.Add("empire");
        }
    }

    // ===================== Kin of the blood (Biotech) =====================

    /// <summary>
    /// People of a colonist's own xenotype heard one of theirs lives here, and come to meet them: from a people whose
    /// folk are of it when there is one, else a few of it living among another. Only a colonist who isn't a baseliner,
    /// of a xenotype of the game's or a mod's (never one made for a single person). Their genes favour a skill, and
    /// that is the lesson they give.
    /// </summary>
    public class HappeningWorker_KinOfBlood : HappeningWorker_AtGate2
    {
        protected override string What => "travelers";

        public static XenotypeDef XenotypeOf(Pawn p)
        {
            var g = p?.genes;
            if (g == null || g.CustomXenotype != null || g.UniqueXenotype) return null;
            var x = g.Xenotype;
            return x == null || x == XenotypeDefOf.Baseliner ? null : x;
        }

        private static IEnumerable<Pawn> Kin(Map map) => Grown(map).Where(p => XenotypeOf(p) != null);

        /// <summary>Folk of this people can be of the xenotype (the people's own set, or one of its kinds').</summary>
        public static bool PeopleOf(Faction f, XenotypeDef x)
        {
            var d = f?.def;
            if (d == null || x == null) return false;
            try
            {
                if (d.xenotypeSet != null && d.xenotypeSet.Contains(x)) return true;
                if (d.basicMemberKind?.xenotypeSet != null && d.basicMemberKind.xenotypeSet.Contains(x)) return true;
                return d.pawnGroupMakers != null && d.pawnGroupMakers.Any(m => m.options != null && m.options.Any(o => o.kind?.xenotypeSet != null && o.kind.xenotypeSet.Contains(x)));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("a people's xenotypes", e);
                return false;
            }
        }

        /// <summary>The skill the xenotype's genes favour most (their aptitudes), if any.</summary>
        public static SkillDef Favoured(XenotypeDef x)
        {
            var sums = new Dictionary<SkillDef, int>();
            if (x?.genes == null) return null;
            foreach (var g in x.genes)
                if (g?.aptitudes != null)
                    foreach (var a in g.aptitudes)
                        if (a?.skill != null)
                            sums[a.skill] = (sums.TryGetValue(a.skill, out int v) ? v : 0) + a.level;
            var best = sums.Where(kv => kv.Value > 0).OrderByDescending(kv => kv.Value).FirstOrDefault();
            return best.Value > 0 ? best.Key : null;
        }

        protected override bool CanHappen(Map map, HappeningExtension ext) => ModsConfig.BiotechActive && Kin(map).Any() && base.CanHappen(map, ext);

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.a = Kin(ctx.map).RandomElementWithFallback();
            var x = XenotypeOf(ctx.a);
            if (x == null) return false;
            ctx.faction = People(f => PeopleOf(f, x) ? 8f : 1f);
            if (ctx.faction == null) return false;
            ctx.tale = x.label;
            var skill = Favoured(x);
            var mine = skill != null ? ctx.a.skills?.GetSkill(skill) : null;
            ctx.skill = mine != null && !mine.TotallyDisabled ? skill : null;
            ctx.variant = ext.count.RandomInRange;
            return true;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            var x = XenotypeOf(ctx.a);
            var t = ComeToGate(ctx, ctx.variant);
            if (t == null) return false;
            // Of the colonist's own blood: their genes as the xenotype's.
            foreach (var p in t.people)
            {
                if (p?.genes == null || x == null || p.genes.Xenotype == x) continue;
                try
                {
                    p.genes.SetXenotype(x);
                    p.Drawer?.renderer?.SetAllGraphicsDirty();
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("kin of the blood", e);
                }
            }
            return true;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            tags.Add(PeopleOf(ctx.faction, XenotypeOf(ctx.a)) ? "theirKind" : "fewOfThem");
            if (ctx.skill != null) tags.Add("skill");
        }
    }

    // ===================== Gawkers at the gravship (Odyssey) =====================

    /// <summary>
    /// Where the colony's gravship comes down a few tiles from a friendly settlement, its folk walk up to see it, a few
    /// hours after the landing (GravshipLanding queues them with the game's own incident queue). Only after a landing,
    /// within three days of it, and never on a map in space.
    /// </summary>
    public class HappeningWorker_GravshipGawkers : HappeningWorker_AtGate2
    {
        protected override string What => "travelers";

        protected override bool CanHappen(Map map, HappeningExtension ext)
        {
            var tr = VisitorsMore2Tracker.Instance;
            return ModsConfig.OdysseyActive && tr != null && tr.landedMap == map.uniqueID && Find.TickManager.TicksGame - tr.landedTick < GenDate.TicksPerDay * 3
                   && VisitorWork2.Engine(map) != null && GravshipLanding.NearSettlement(map) != null && base.CanHappen(map, ext);
        }

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var s = GravshipLanding.NearSettlement(ctx.map);
            var engine = VisitorWork2.Engine(ctx.map);
            if (s == null || engine == null || !Visitors.Usable(s.Faction)) return false;
            ctx.faction = s.Faction;
            ctx.placeName = s.Label;
            ctx.place = engine;
            ctx.tale = GravshipUtility.TryGetNameOfGravshipOnMap(ctx.map, out var name) && !name.NullOrEmpty() ? name : "CC_V2_TheShip".Translate().Resolve();
            ctx.a = Grown(ctx.map).OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0).FirstOrDefault();
            ctx.variant = ext.count.RandomInRange;
            ctx.count = Mathf.RoundToInt(ext.amount.RandomInRange / 10f) * 10;
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            if (ComeToGate(ctx, ctx.variant) == null) return false;
            // Once a landing.
            var tr = VisitorsMore2Tracker.Instance;
            if (tr != null) tr.landedTick = -1;
            return true;
        }
    }

    /// <summary>
    /// The gawkers' goods for the silver the colony carries out: the settlement's things of the colony's own era, worth
    /// <see cref="factor"/> times {COUNT}, set down at the gate by the one of them who is here (Couriers).
    /// </summary>
    public class DilemmaEffect_GawkersGoods : DilemmaEffect
    {
        public float factor = 1.4f;

        public override void Apply(DilemmaContext ctx)
        {
            var map = ctx?.map;
            if (map == null || ctx.count <= 0) return;
            var by = new[] { ctx.stranger }.Concat((ctx as HappeningContext)?.group ?? new List<Pawn>()).FirstOrDefault(p => p != null && !p.Dead && p.Spawned && p.Map == map);
            var things = Loot.Make(ctx.count * factor);
            // together: set down by the one of them who is here.
            Couriers.Loot(things, map, by?.Position ?? DropCellFinder.TradeDropSpot(map), Couriers.PeopleOf(ctx), by);
        }

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (ctx?.map == null || ctx.count <= 0) return;
            sheet.Add(ImpactKind.Goods, 1, "CC_Impact_V2Goods".Translate(Mathf.RoundToInt(ctx.count * factor)).Resolve(), "CC_Impact_V2GoodsBrief".Translate().Resolve());
        }
    }

    // ===================== The book swapper =====================

    /// <summary>
    /// A reader with a pack of books offers one of theirs for one of the colony's, named by its title. Only when the
    /// colony has a book (on a bookcase or in storage). Theirs is a real book in their pack; the colony's is taken from
    /// the shelf by their own hand (JobDriver_VisitSwap).
    /// </summary>
    public class HappeningWorker_BookSwapper : HappeningWorker_AtGate2
    {
        protected override string What => "traveler";

        private static readonly string[] TheirKinds = { "Novel", "TextBook", "Schematic" };

        private static List<ThingDef> Kinds() =>
            TheirKinds.Select(n => DefDatabase<ThingDef>.GetNamedSilentFail(n)).Where(d => d != null && typeof(Book).IsAssignableFrom(d.thingClass)
                                                                                     && (d.techLevel == TechLevel.Undefined || d.techLevel <= Avail.EffectiveTech)).ToList();

        protected override bool CanHappen(Map map, HappeningExtension ext) => VisitorWork2.Books(map).Count > 0 && Kinds().Count > 0 && base.CanHappen(map, ext);

        /// <summary>The colony's book they ask for, between casting and their walking in (the same moment).</summary>
        private Thing wanted;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            wanted = VisitorWork2.Books(ctx.map).RandomElementWithFallback();
            if (wanted == null) return false;
            ctx.faction = People(Peoples.Weigh(ext.prefers));
            if (ctx.faction == null) return false;
            ctx.thing = wanted.def;
            ctx.tale = VisitorWork2.Title(wanted);
            ctx.count = Mathf.Max(20, Mathf.RoundToInt(wanted.MarketValue * 1.2f / 5f) * 5);
            ctx.a = Grown(ctx.map).OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Intellectual)?.Level ?? 0).FirstOrDefault();
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            var t = ComeToGate(ctx, 1);
            if (t == null || wanted == null) return false;
            var x = ctx.stranger;
            Thing theirs = null;
            try
            {
                theirs = ThingMaker.MakeThing(Kinds().RandomElement());
                theirs.TryGetComp<CompQuality>()?.SetQuality((QualityCategory)Rand.RangeInclusive((int)QualityCategory.Poor, (int)QualityCategory.Excellent), ArtGenerationContext.Outsider);
                if (x?.inventory == null || !x.inventory.innerContainer.TryAdd(theirs))
                {
                    if (!theirs.Destroyed) theirs.Destroy();
                    theirs = null;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the book swapper's own book", e);
            }
            if (theirs == null)
            {
                VisitorTracker.Instance?.SendHome(t);
                return false;
            }
            ctx.placeName = VisitorWork2.Title(theirs);
            int mine = wanted.TryGetQuality(out var qa) ? (int)qa : 2, other = theirs.TryGetQuality(out var qb) ? (int)qb : 2;
            ctx.variant = Math.Sign(other - mine);
            VisitorsMore2Tracker.Instance?.Add(new BookDeal { tripId = t.id, by = x, want = wanted, theirs = theirs, silver = ctx.count });
            return true;
        }

        protected override void Abort(HappeningContext ctx)
        {
            wanted = null;
            base.Abort(ctx);
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            if (ctx.variant > 0) tags.Add("theirsBetter");
            else if (ctx.variant < 0) tags.Add("theirsWorse");
        }
    }

    /// <summary>
    /// The answer to the book swapper: "swap" (they take the colony's book from the shelf and leave theirs in its place),
    /// "sell" (they take it and leave {COUNT} silver), or "copy" (they copy it at the bookcase and leave it be).
    /// </summary>
    public class DilemmaEffect_BookDeal : DilemmaEffect
    {
        public string mode = "swap";

        private static BookDeal Deal(DilemmaContext ctx) => VisitorsMore2Tracker.Instance?.DealOf(VisitorTracker.Instance?.WaitingFor(ctx));

        public override string Unavailable(DilemmaContext ctx)
        {
            var d = Deal(ctx);
            if (d == null) return "CC_V2_Book_NoOne".Translate().Resolve();
            return VisitorWork2.PlaceOf(d.want) == null ? "CC_V2_Book_Gone".Translate(VisitorWork2.Title(d.want)).Resolve() : null;
        }

        public override void Apply(DilemmaContext ctx)
        {
            var d = Deal(ctx);
            if (d != null && !d.done) d.mode = mode;
        }

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var d = Deal(ctx);
            if (d == null) return;
            string book = VisitorWork2.Title(d.want);
            switch (mode)
            {
                case "sell":
                    sheet.Silver(d.silver);
                    sheet.Add(ImpactKind.Goods, -1, "CC_Impact_V2BookGoes".Translate(book).Resolve(), "CC_Impact_V2BookGoesBrief".Translate().Resolve());
                    break;
                case "swap":
                    sheet.Add(ImpactKind.Goods, 0, "CC_Impact_V2BookSwap".Translate(book, VisitorWork2.Title(d.theirs)).Resolve(), "CC_Impact_V2BookSwapBrief".Translate().Resolve());
                    break;
                default:
                    sheet.Play("CC_Impact_V2BookCopy".Translate(book).Resolve(), "CC_Impact_V2BookCopyBrief".Translate().Resolve());
                    break;
            }
        }
    }

    // ===================== Regulars' own lives =====================

    /// <summary>Which turn of a regular's life an event tells: "wed", "child", "apprentice", "died".</summary>
    public class RegularLifeExtension : DefModExtension
    {
        public string turn;
    }

    /// <summary>
    /// A regular the colony knows well (three visits, a warm bond) comes back changed: married, with a child they took
    /// in on the road, or old and bringing the apprentice who will carry their trade on; or a fellow traveller brings
    /// word that they died on the road, only when there was a real cause (great age, an illness they carried). A
    /// keepsake for their friend is set down at the gate (the event's own effects).
    /// </summary>
    public class HappeningWorker_RegularLife : HappeningWorker_AtGate2
    {
        protected override string What => "travelers";

        private string Turn => def.GetModExtension<RegularLifeExtension>()?.turn;

        /// <summary>The regular whose death the word tells, for their keepsake set down with the letter (the same moment).</summary>
        public static Pawn LastDead;

        private static bool Married(Pawn p) => p?.relations?.GetFirstDirectRelationPawn(PawnRelationDefOf.Spouse) != null;

        /// <summary>What a regular's death on the road came of, when anything real would: great age, or an illness that kills that they carried.</summary>
        public static string MortalCause(Pawn p)
        {
            if (p?.ageTracker == null) return null;
            if (RegularLifeMath.Old(p.ageTracker.AgeBiologicalYearsFloat)) return "CC_V2_Regular_OldAge".Translate().Resolve();
            var ill = p.health?.hediffSet?.hediffs?.FirstOrDefault(h => h?.def != null && h.def.chronic && h.def.lethalSeverity > 0f);
            return ill?.LabelBase;
        }

        private bool Fits(Regular r)
        {
            var p = r.pawn;
            float age = p.ageTracker?.AgeBiologicalYearsFloat ?? 30f;
            switch (Turn)
            {
                case "wed": return !Married(p) && age >= 20f && age < 60f;
                case "child": return age >= 22f && age < 55f && (!ModsConfig.BiotechActive || Find.Storyteller.difficulty.ChildrenAllowed);
                case "apprentice": return RegularLifeMath.Elder(age) && r.Skill != null;
                case "died": return MortalCause(p) != null;
                default: return false;
            }
        }

        private Regular Pick() =>
            Regulars.All.Where(r => r.Alive && !r.pawn.Spawned && WorldSituationWorker.FreeInWorld(r.pawn) && RegularMath.Due(r.DaysSince) && RegularLifeMath.Warm(r.visits, r.bond)
                                    && Visitors.Usable(r.faction) && Fits(r))
                .OrderByDescending(r => r.bond).FirstOrDefault();

        protected override bool CanHappen(Map map, HappeningExtension ext) => Pick() != null && base.CanHappen(map, ext);

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var r = Pick();
            if (r == null) return false;
            ctx.faction = r.faction;
            var friend = r.friendOf;
            ctx.a = friend != null && !friend.Dead && friend.Spawned && friend.Map == ctx.map && friend.IsColonist && !friend.Downed ? friend : Grown(ctx.map).RandomElementWithFallback();
            if (ctx.a == null) return false;
            ctx.tale = ("CC_Regular_Kind_" + r.kind).Translate();
            ctx.skill = r.Skill;
            ctx.variant = r.id;
            ctx.dead = r.pawn.LabelShort;
            if (Turn == "died") ctx.relation = MortalCause(r.pawn);
            return true;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            var r = Regulars.All.FirstOrDefault(x => x.id == ctx.variant);
            if (r == null) return false;
            VisitorTrip t;
            switch (Turn)
            {
                case "wed":
                    t = ComeToGate(ctx, 1, r.pawn, 0, new FloatRange(20f, 45f));
                    break;
                case "child":
                    t = ComeToGate(ctx, 1, r.pawn, 1);
                    break;
                case "apprentice":
                    t = ComeToGate(ctx, 1, r.pawn, 0, new FloatRange(16f, 24f));
                    break;
                case "died":
                    t = ComeToGate(ctx, 1);
                    break;
                default:
                    return false;
            }
            if (t == null) return false;
            var other = ctx.group.FirstOrDefault();
            if (Turn == "died")
            {
                // The word they bring is true: the regular is gone from the world, and the colony no longer waits for them.
                LastDead = r.pawn;
                WorldSituationWorker.DieOffscreen(r.pawn);
                Regulars.Forget(r);
                return true;
            }
            if (other == null) return false;
            ctx.placeName = other.LabelShort;
            if (Turn == "wed")
            {
                try
                {
                    r.pawn.relations?.AddDirectRelation(PawnRelationDefOf.Spouse, other);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("a regular's wedding", e);
                }
            }
            r.visits++;
            r.lastTick = Find.TickManager.TicksGame;
            return true;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            var r = Regulars.All.FirstOrDefault(x => x.id == ctx.variant);
            if (r != null) tags.Add(r.kind);
            if (r?.friendOf != null && r.friendOf == ctx.a) tags.Add("friend");
            if (Turn == "died") tags.Add(ctx.relation == "CC_V2_Regular_OldAge".Translate().Resolve() ? "old" : "ill");
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            // A warm first answer tightens the thread with the one who came back.
            if (option == 0 && !byDefault && Turn != "died") Regulars.Warm(ctx.stranger);
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The old regular's apprentice (the one who came with them) becomes the regular the colony keeps up with, in their trade, and the old one goes home to rest.</summary>
    public class DilemmaEffect_RegularHeir : DilemmaEffect
    {
        private static (Regular r, Pawn heir) Find(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            var r = Regulars.All.FirstOrDefault(x => hc != null && x.id == hc.variant && x.pawn == ctx.stranger);
            var heir = hc?.group.FirstOrDefault(p => p != null && !p.Dead && p.RaceProps.Humanlike && p.Faction == r?.faction);
            return (r, heir);
        }

        public override string Unavailable(DilemmaContext ctx) => Find(ctx).heir == null ? "CC_Regular_Gone".Translate().ToString() : null;

        public override void Apply(DilemmaContext ctx)
        {
            var (r, heir) = Find(ctx);
            if (r == null || heir == null) return;
            r.pawn = heir;
            r.visits = 1;
            r.bond = Math.Max(1, r.bond / 2);
            r.lastTick = Verse.Find.TickManager.TicksGame;
            Verse.Find.WorldPawns.ForcefullyKeptPawns.Add(heir);
        }

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var (r, heir) = Find(ctx);
            if (r == null || heir == null) return;
            sheet.Add(ImpactKind.People, 1, "CC_Impact_V2Heir".Translate(heir.LabelShort, ("CC_Regular_Kind_" + r.kind).Translate()).Resolve(), "CC_Impact_V2HeirBrief".Translate().Resolve());
        }
    }

    /// <summary>
    /// A keepsake from a regular for their friend (A): a small wooden carving of good make, signed by the regular, carried
    /// in by the one of them at the gate and set down there (Couriers: handed over as they arrive). With
    /// <see cref="ofDead"/>, it was the dead regular's, brought by the one who carries the word. A carving of their
    /// people's age (Things/ThingRoles.cs: a small sculpture, or a mod's small statue or pottery).
    /// </summary>
    public class DilemmaEffect_Keepsake : DilemmaEffect
    {
        public bool ofDead;
        public QualityCategory quality = QualityCategory.Good;

        private static ThingDef Carving(DilemmaContext ctx)
        {
            var wood = ThingDefOf.WoodLog;
            if (wood?.stuffProps == null) return null;
            return ThingRoles.From(ThingRole.Carving, Couriers.PeopleOf(ctx), ctx?.map, 0, d => d.Minifiable && wood.stuffProps.CanMake(d));
        }

        public override void Apply(DilemmaContext ctx)
        {
            var map = ctx?.map;
            var x = ctx?.stranger;
            var def = Carving(ctx);
            if (map == null || x == null || !x.Spawned || def == null) return;
            try
            {
                var art = ThingMaker.MakeThing(def, ThingDefOf.WoodLog);
                var maker = ofDead ? HappeningWorker_RegularLife.LastDead : x;
                if (maker != null) art.TryGetComp<CompArt>()?.JustCreatedBy(maker);
                art.TryGetComp<CompQuality>()?.SetQuality(quality, ArtGenerationContext.Outsider);
                // together: carried in by the one of them at the gate and set down there.
                Couriers.Loot(new List<Thing> { MinifyUtility.MakeMinified(art) }, map, PlaySpots.GateCell(map, null), Couriers.PeopleOf(ctx), x);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("a regular's keepsake", e);
            }
        }

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (ctx?.map == null || ctx.stranger == null || Carving(ctx) == null) return;
            sheet.Add(ImpactKind.Goods, 1, "CC_Impact_V2Keepsake".Translate(ctx.a?.LabelShort ?? "?").Resolve(), "CC_Impact_V2KeepsakeBrief".Translate().Resolve());
        }
    }
}
