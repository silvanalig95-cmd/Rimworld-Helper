using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // Visitors who come to the colony to do something (Visitors/Callers.cs does it on the map): the art collector, a
    // wedding party, the undertakers, a captive's kin, the traveling barber, the animal doctor. Each comes only when the
    // colony has what they come for; the defs are in Defs/IncidentDefs/Rimfolk_Events_Callers.xml.

    /// <summary>What the callers' events share: the answer's words handed to the work they set going.</summary>
    public abstract class HappeningWorker_Callers : HappeningWorker_Visitors
    {
        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            outcome = base.Resolved(ctx, option, risk, byDefault, outcome);
            // What the answer set going for the visitors tells its words when it's done (Callers).
            if (!outcome.NullOrEmpty() && Callers.Adopt(ctx, ctx.Format(outcome))) return "";
            return outcome;
        }

        /// <summary>Where visitors wait for the answer: the colony's gate.</summary>
        protected static IntVec3? Gate(Map map)
        {
            var c = PlaySpots.GateCell(map, null);
            return c.IsValid ? c : (IntVec3?)null;
        }

        /// <summary>The colonist whose name a piece of art carries, if they're one of the colony.</summary>
        public static Pawn MakerOf(Thing piece, Map map)
        {
            var art = (piece as MinifiedThing)?.InnerThing.TryGetComp<CompArt>() ?? piece?.TryGetComp<CompArt>();
            string author = art?.AuthorName.ToString();
            if (author.NullOrEmpty() || map == null) return null;
            return map.mapPawns.FreeColonistsSpawned.FirstOrDefault(p => p.Name != null && author.Contains(p.Name.ToStringFull));
        }
    }

    // ===================== The art collector =====================

    /// <summary>
    /// A collector comes to see the colony's art (three works of Good quality or better) and offers for one piece by its
    /// title: they walk round the rooms, stopping before each piece. Sold, it's taken up and carried out to them; kept,
    /// word of it travels with them; commissioned, the colony's sculptor makes one at the bench and they come back for it.
    /// </summary>
    public class HappeningWorker_Collector : HappeningWorker_Callers
    {
        /// <summary>Sculptures of the colony's of Good quality or better: standing in its rooms, or packed in its stores.</summary>
        public static List<Thing> Art(Map map)
        {
            var list = new List<Thing>();
            if (map == null) return list;
            foreach (var b in map.listerBuildings.allBuildingsColonist)
                if (Good(b) && !b.IsBurning()) list.Add(b);
            foreach (var m in map.listerThings.ThingsInGroup(ThingRequestGroup.MinifiedThing))
                if (m is MinifiedThing mt && Good(mt.InnerThing) && m.Spawned && m.IsInAnyStorage() && !m.IsForbidden(Faction.OfPlayer)) list.Add(m);
            return list;
        }

        private static bool Good(Thing t) =>
            t?.def != null && t.def.IsArt && t.def.Minifiable && (t.def.thingCategories?.Contains(ThingCategoryDefOf.BuildingsArt) ?? false)
            && t.TryGetQuality(out var q) && q >= QualityCategory.Good && (t.TryGetComp<CompArt>()?.Active ?? false);

        private static Thing Inner(Thing t) => (t as MinifiedThing)?.InnerThing ?? t;

        protected override bool CanHappen(Map map, HappeningExtension ext) => base.CanHappen(map, ext) && Art(map).Count >= CallerMath.MinGoodArt;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = Arrivals.Friendly();
            var art = Art(ctx.map);
            if (ctx.faction == null || art.Count < CallerMath.MinGoodArt) return false;
            // One of the finest three, by what it's worth.
            var piece = art.OrderByDescending(t => t.MarketValue).Take(3).RandomElement();
            MakeGroup(ctx, ext, 1, ext.ages, null, false);
            if (ctx.stranger == null) return false;
            var inner = Inner(piece);
            ctx.place = piece;
            ctx.thing = inner.def;
            ctx.tale = inner.TryGetComp<CompArt>()?.Title ?? inner.LabelNoCount;
            ctx.count = CallerMath.SalePrice(inner.MarketValue);
            var maker = MakerOf(piece, ctx.map);
            ctx.a = maker != null && !maker.Downed ? maker : Colonists(ctx.map).Where(p => !p.DevelopmentalStage.Juvenile()).RandomElementWithFallback();
            ctx.b = DilemmaEffect_ArtCommission.Sculptor(ctx.map);
            ctx.note = maker == ctx.a && maker != null ? "maker" : null;
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            if (Arrivals.Visit(new List<Pawn> { ctx.stranger }, ctx.map, ctx.faction, null, ext.stayHours.RandomInRange) == null) return false;
            // A walk round the art while the colony makes up its mind, nearest first.
            var art = Art(ctx.map).OrderBy(t => t.Position.DistanceToSquared(ctx.stranger.Position)).Take(6).ToList();
            if (!art.Contains(ctx.place)) art.Add(ctx.place);
            Callers.View(ctx.map, ctx.stranger, art, def.defName);
            return true;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            if (ctx.note == "maker") tags.Add("maker");
            if (ctx.place is Building) tags.Add("standing");
            if (ctx.b == null) tags.Add("nosculptor");
        }

        protected override string Extra(HappeningContext ctx)
        {
            var inner = Inner(ctx.place);
            if (inner == null) return null;
            inner.TryGetQuality(out var q);
            return "CC_Caller_Collector_Extra".Translate(ctx.tale.Named("TITLE"), q.GetLabel().Named("QUALITY"), Mathf.RoundToInt(inner.MarketValue).Named("VALUE"),
                (inner.TryGetComp<CompArt>()?.AuthorName.ToString() ?? "?").Named("AUTHOR")).Resolve();
        }
    }

    /// <summary>The collector comes back for the piece the colony made for them, and pays for it by its quality.</summary>
    public class HappeningWorker_CollectorBack : HappeningWorker_Callers
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) => Colonists(map).Count > 0 && Callers.Due(map) != null;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var c = Callers.Due(ctx.map);
            if (c == null) return false;
            var piece = Callers.Outer(c.made);
            ctx.stranger = c.collector;
            ctx.faction = c.faction;
            ctx.group.Clear();
            ctx.place = piece;
            ctx.thing = c.def;
            ctx.variant = c.id;
            c.made.TryGetQuality(out var q);
            ctx.tale = q.GetLabel();
            ctx.count = CallerMath.CommissionPrice(c.made.MarketValue);
            var maker = c.maker;
            ctx.a = maker != null && !maker.Dead && maker.IsColonist && maker.Spawned && maker.Map == ctx.map ? maker
                : Colonists(ctx.map).Where(p => !p.DevelopmentalStage.Juvenile()).RandomElementWithFallback();
            ctx.note = maker == ctx.a ? "maker" : null;
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(new List<Pawn> { ctx.stranger }, ctx.map, ctx.faction, Gate(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void Abort(HappeningContext ctx)
        {
            // Theirs to collect: a collector who couldn't come is still owed it.
            ctx.stranger = null;
            ctx.group.Clear();
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            if (ctx.note == "maker") tags.Add("maker");
        }
    }

    /// <summary>
    /// The piece ({PLACE}: the event's thing) is carried out to the visitor X, taken up first if it stands in a room, and
    /// X pays {COUNT} silver into the carrier's hands. Its maker is proud of the sale, or hurt by it if the work was close
    /// to their heart (an artist by passion, not a greedy one). With <see cref="commission"/>, it settles the commission
    /// the event names (its variant).
    /// </summary>
    public class DilemmaEffect_SellArt : DilemmaEffect
    {
        public bool commission;

        private static bool Hurt(Pawn maker)
        {
            if (maker?.skills == null || maker.story?.traits == null) return false;
            if (maker.story.traits.HasTrait(TraitDefOf.Greedy)) return false;
            return maker.skills.GetSkill(SkillDefOf.Artistic)?.passion == Passion.Major;
        }

        public override string Unavailable(DilemmaContext ctx)
        {
            var piece = (ctx as HappeningContext)?.place;
            if (piece == null || piece.Destroyed || piece.MapHeld != ctx.map) return "CC_Caller_ArtGone".Translate().Resolve();
            if (ctx.stranger == null || !ctx.stranger.Spawned) return "CC_Regular_Gone".Translate().Resolve();
            return null;
        }

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var piece = (ctx as HappeningContext)?.place;
            if (piece == null || ctx.stranger == null) return;
            sheet.Play("CC_Impact_Caller_SellArt".Translate(PlayGive.Label(piece).Named("THING"), ctx.stranger.Named("OTHER")).Resolve(), "CC_Impact_Caller_SellArtBrief".Translate().Resolve());
            sheet.Goods(-1, PlayGive.Label(piece), 1, "art|" + piece.thingIDNumber);
            sheet.Silver(ctx.count);
            var maker = HappeningWorker_Callers.MakerOf(piece, ctx.map);
            if (maker != null) sheet.Thought(new List<Pawn> { maker }, maker.LabelShort, Hurt(maker) && !commission ? CallerDefOf.Rimfolk_Evt_ArtSoldHurt : CallerDefOf.Rimfolk_Evt_ArtSoldProud, _ => null);
        }

        public override void Apply(DilemmaContext ctx)
        {
            var piece = (ctx as HappeningContext)?.place;
            var x = ctx.stranger;
            var map = ctx.map;
            if (piece == null || piece.Destroyed || map == null || x == null) return;
            var maker = HappeningWorker_Callers.MakerOf(piece, map);
            if (maker != null)
                // apart: a feeling about their own work leaving the colony, wherever they are.
                CouncilGameComponent.GiveThought(maker, Hurt(maker) && !commission ? CallerDefOf.Rimfolk_Evt_ArtSoldHurt : CallerDefOf.Rimfolk_Evt_ArtSoldProud);
            int id = commission ? (ctx as HappeningContext)?.variant ?? -1 : -1;
            var t = Callers.Hand(map, x, piece, null, ctx.count, null, (ctx as HappeningContext)?.incident?.defName, id);
            if (t != null)
            {
                t.ctxRef = ctx;
                return;
            }
            // Nothing played out (the setting off, the collector gone): it goes with them, and they pay, as answers always had it.
            if (x.Spawned && x.inventory != null)
            {
                var outer = Callers.Outer(piece);
                if (outer.Spawned) outer.DeSpawn();
                else outer.holdingOwner?.Remove(outer);
                if (!(outer is MinifiedThing) && outer.def.Minifiable) outer = outer.MakeMinified();
                x.inventory.innerContainer.TryAdd(outer);
            }
            else Callers.Outer(piece).Destroy();
            Couriers.PaySilver(map, x.Spawned ? x.Position : IntVec3.Invalid, ctx.count, x.Faction, x.Spawned ? x : null);
            if (id >= 0) Callers.Settled(id);
        }
    }

    /// <summary>The piece stays; the collector takes word of the colony's art with them, and the colony's name for it grows.</summary>
    public class DilemmaEffect_ArtWord : DilemmaEffect
    {
        public float amount = 3f;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (ctx?.stranger != null) sheet.Add(ImpactKind.Other, 1, "CC_Impact_Caller_ArtWord".Translate(ctx.stranger.Named("OTHER")).Resolve(), "CC_Impact_Caller_ArtWordBrief".Translate().Resolve());
        }

        public override void Apply(DilemmaContext ctx) => ColonyTraits.Tally("seen:art", amount, ctx?.map);
    }

    /// <summary>
    /// A commission: the colony's sculptor (B) makes a small sculpture for the collector X at the sculptor's table, a
    /// day's work, from the colony's own wood, stone or metal carried to the bench; X comes back for it later and pays by
    /// its quality (Callers' commissions, HappeningWorker_CollectorBack).
    /// </summary>
    public class DilemmaEffect_ArtCommission : DilemmaEffect
    {
        public string things = "SculptureSmall";

        /// <summary>The colony's best sculptor: grown, able to do art.</summary>
        public static Pawn Sculptor(Map map) =>
            IncidentWorker_Happening.Colonists(map).Where(p => !p.DevelopmentalStage.Juvenile() && !p.Downed && p.skills?.GetSkill(SkillDefOf.Artistic) is SkillRecord r && !r.TotallyDisabled
                                                               && !p.WorkTypeIsDisabled(WorkTypeDefOf.Hauling))
                .OrderByDescending(p => p.skills.GetSkill(SkillDefOf.Artistic).Level).FirstOrDefault();

        private ThingDef Def => DilemmaEffect_Made.Pick(things);

        /// <summary>A sculptor's table of the colony's (any bench the sculpture is made at).</summary>
        public static bool Bench(Map map, ThingDef def)
        {
            var recipe = PlayMake.RecipeFor(def);
            if (recipe == null || map == null) return false;
            foreach (var user in recipe.AllRecipeUsers)
                foreach (var b in map.listerThings.ThingsOfDef(user))
                    if (b.Faction == Faction.OfPlayer && b.def.category == ThingCategory.Building) return true;
            return false;
        }

        /// <summary>The plainest stuff in the stores enough for it: wood, stone blocks, steel before anything dear.</summary>
        public static Thing Stuff(Map map, ThingDef def)
        {
            if (map == null || def == null || !def.MadeFromStuff) return null;
            int need = Mathf.Max(1, def.CostStuffCount);
            return map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver)
                .Where(t => t.def.IsStuff && t.def.stuffProps.CanMake(def) && t.stackCount >= need && t.Spawned && t.IsInAnyStorage() && !t.IsForbidden(Faction.OfPlayer))
                .OrderBy(t => t.def.BaseMarketValue).FirstOrDefault();
        }

        public override string Unavailable(DilemmaContext ctx)
        {
            var def = Def;
            var map = ctx?.map;
            if (def == null || map == null) return "CC_Caller_NoSculptor".Translate().Resolve();
            if (ctx.b == null || Sculptor(map) == null) return "CC_Caller_NoSculptor".Translate().Resolve();
            if (!Bench(map, def)) return "CC_Caller_NoBench".Translate().Resolve();
            if (Stuff(map, def) == null) return "CC_Dilemma_NotEnough".Translate(def.CostStuffCount, "CC_Caller_Stuff".Translate()).Resolve();
            return null;
        }

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var def = Def;
            var stuff = Stuff(ctx?.map, def);
            if (def == null || ctx.b == null || stuff == null || ctx.stranger == null) return;
            sheet.Play("CC_Impact_Caller_Commission".Translate(ctx.b.Named("PAWN"), def.label.Named("THING"), CallerMath.CommissionHours.Named("HOURS"), ctx.stranger.Named("OTHER")).Resolve(),
                "CC_Impact_Caller_CommissionBrief".Translate().Resolve());
            sheet.Goods(-1, stuff.def, def.CostStuffCount);
        }

        public override void Apply(DilemmaContext ctx)
        {
            var def = Def;
            var map = ctx?.map;
            var maker = ctx?.b;
            var x = ctx?.stranger;
            if (def == null || map == null || maker == null || x == null) return;
            var stack = Stuff(map, def);
            if (stack == null) return;
            try
            {
                // What it's made of, set apart from the rest of the stack beside it, kept for the sculptor to carry to the bench.
                int need = Mathf.Max(1, def.CostStuffCount);
                Thing from = stack;
                if (stack.stackCount > need)
                {
                    from = stack.SplitOff(need);
                    var at = stack.Position;
                    var cell = GenAdj.CellsAdjacent8Way(stack).Where(c => c.InBounds(map) && c.Standable(map) && c.GetFirstItem(map) == null).FirstOrDefault(c => c.IsValid);
                    GenSpawn.Spawn(from, cell.IsValid && cell.InBounds(map) && cell != default ? cell : at, map);
                }
                from.SetForbidden(true, false);
                var play = PlayMake.KeepMade(map, maker, def, from.def.defName, from, -1, 1, "CC_Caller_Commission_Label".Translate(x.Named("OTHER")).Resolve(), SkillDefOf.Artistic, null);
                if (play == null)
                {
                    from.SetForbidden(false, false);
                    if (PlayOuts.On) return;
                    // "Answers play out on the map" off: it's made at once, from the stuff set apart, as answers used to.
                    var made = DilemmaEffect_Made.Make(def, from.def.defName, QualityCategory.Normal, maker, SkillDefOf.Artistic);
                    if (made == null) return;
                    var at = maker.Spawned && maker.Map == map ? maker.Position : from.Position;
                    from.Destroy();
                    if (!GenPlace.TryPlaceThing(made.def.Minifiable ? made.MakeMinified() : made, at, map, ThingPlaceMode.Near, out var lying)) return;
                    var owed = Callers.Commission(x, maker, def, -1, map);
                    if (owed != null)
                    {
                        owed.made = (lying as MinifiedThing)?.InnerThing ?? lying;
                        owed.madeTick = Find.TickManager.TicksGame;
                    }
                    return;
                }
                play.makeTicks = PlayOutMath.DurationTicks(CallerMath.CommissionHours);
                PlayOuts.Register(play);
                Callers.Commission(x, maker, def, play.id, map);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("art commission", e);
            }
        }
    }

    /// <summary>The colony keeps the commissioned piece after all: the commission is over, nothing paid.</summary>
    public class DilemmaEffect_KeepCommission : DilemmaEffect
    {
        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var piece = (ctx as HappeningContext)?.place;
            if (piece != null) sheet.Add(ImpactKind.Goods, 1, "CC_Impact_Caller_KeepCommission".Translate(PlayGive.Label(piece).Named("THING")).Resolve());
        }

        public override void Apply(DilemmaContext ctx) => Callers.Settled((ctx as HappeningContext)?.variant ?? -1);
    }

    // ===================== A wedding at the colony =====================

    /// <summary>
    /// A couple of a neighbouring people (goodwill 20 or more) asks to marry at the colony, with their families: a feast
    /// (cooked at the colony's stove, or from the food they bring) with the families as guests, at the colony's marriage
    /// spot or dining table, and a night there.
    /// </summary>
    public class HappeningWorker_Wedding : HappeningWorker_Callers
    {
        private static bool Fits(Faction f) => f != null && f.PlayerGoodwill >= CallerMath.WeddingGoodwill && Arrivals.PersonKind(f) != null;

        private static Thing MarriageSpot(Map map)
        {
            var def = DefDatabase<ThingDef>.GetNamedSilentFail("MarriageSpot");
            return def == null ? null : map.listerThings.ThingsOfDef(def).FirstOrDefault(t => t.Faction == Faction.OfPlayer);
        }

        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            Colonists(map).Count > 0 && Arrivals.Friendly(Fits) != null && (MarriageSpot(map) != null || RoomUtil.RoomsWithRole(map, RoomUtil.DiningRoom).Any());

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = Arrivals.Friendly(Fits);
            var kind = Arrivals.PersonKind(ctx.faction);
            if (kind == null) return false;
            var list = new List<Pawn>();
            try
            {
                var one = Arrivals.Make(kind, ctx.faction, new FloatRange(20f, 32f));
                var two = Arrivals.Make(kind, ctx.faction, new FloatRange(20f, 32f), one.gender == Gender.Male ? Gender.Female : Gender.Male);
                one.relations?.AddDirectRelation(PawnRelationDefOf.Fiance, two);
                list.Add(one);
                list.Add(two);
                // A parent of each, sharing their name; sometimes a younger sister or brother of the first.
                foreach (var child in new[] { one, two })
                {
                    float age = child.ageTracker.AgeBiologicalYearsFloat;
                    var parent = Arrivals.Make(kind, ctx.faction, new FloatRange(Mathf.Min(age + 22f, 75f), Mathf.Min(age + 32f, 80f)), null, (child.Name as NameTriple)?.Last);
                    child.relations?.AddDirectRelation(PawnRelationDefOf.Parent, parent);
                    list.Add(parent);
                    if (child == one && Rand.Chance(0.5f))
                    {
                        var sib = Arrivals.Make(kind, ctx.faction, new FloatRange(16f, Mathf.Max(17f, age)), null, (child.Name as NameTriple)?.Last);
                        sib.relations?.AddDirectRelation(PawnRelationDefOf.Parent, parent);
                        list.Add(sib);
                    }
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("wedding party", e);
                foreach (var p in list) Arrivals.Discard(p);
                return false;
            }
            ctx.stranger = list[0];
            ctx.group = list.Skip(1).ToList();
            ctx.tale = list[1].LabelShort;
            ctx.place = MarriageSpot(ctx.map);
            ctx.count = CallerMath.FeastFood(Colonists(ctx.map).Count + list.Count);
            ctx.variant = Mathf.RoundToInt(ctx.count * 0.8f);
            ctx.a = Colonists(ctx.map).Where(p => !p.DevelopmentalStage.Juvenile()).OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0).FirstOrDefault();
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(ctx.group.Prepend(ctx.stranger).ToList(), ctx.map, ctx.faction, Gate(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            if (ctx.place != null) tags.Add("spot");
            if (ModsConfig.IdeologyActive) tags.Add("ideology");
        }

        protected override string Extra(HappeningContext ctx) => "CC_Caller_Wedding_Extra".Translate(ctx.count, ctx.variant, ctx.group.Count + 1).Resolve();
    }

    /// <summary>
    /// The wedding party's night at the colony (Callers.Night): under its roof (<see cref="inside"/>: the colony's
    /// spare beds, the couple sharing one, else the floor of a warm room), or round a fire of their own beyond the wall.
    /// </summary>
    public class DilemmaEffect_WeddingNight : DilemmaEffect
    {
        public bool inside = true;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (ctx?.stranger == null || !ctx.stranger.Spawned) return;
            sheet.Play((inside ? "CC_Impact_Caller_NightInside" : "CC_Impact_Caller_NightFire").Translate().Resolve(), "CC_Impact_Caller_NightBrief".Translate().Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            if (hc?.stranger == null || ctx.map == null) return;
            Callers.Night(ctx.map, hc.group.Prepend(hc.stranger).ToList(), inside, hc.incident?.defName);
        }
    }

    /// <summary>
    /// The wedding party brings the food for its feast: {COUNT} raw food of theirs set down at the colony by X, and the
    /// feast the same answer set going is cooked at the colony's stove from as much (PlayCook).
    /// </summary>
    public class DilemmaEffect_GuestsBringFood : DilemmaEffect
    {
        public string things = "RawPotatoes|RawRice|RawCorn|RawBerries";

        private static ThingDef Food(string names) =>
            names.Split('|').Select(n => DefDatabase<ThingDef>.GetNamedSilentFail(n.Trim())).FirstOrDefault(d => d != null && d.IsNutritionGivingIngestible);

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var def = Food(things);
            if (def == null || ctx?.stranger == null || ctx.count <= 0) return;
            sheet.Play("CC_Impact_Caller_BringFood".Translate(ctx.stranger.Named("OTHER"), ctx.count, def.label).Resolve(), "CC_Impact_Caller_BringFoodBrief".Translate().Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            var def = Food(things);
            var x = ctx?.stranger;
            if (def == null || x == null || ctx.map == null || ctx.count <= 0) return;
            // together: set down at the colony by the one of them who is here.
            Couriers.Hand(def, ctx.count, ctx.map, x.Spawned ? x.Position : IntVec3.Invalid, Couriers.PeopleOf(ctx), x.Spawned ? x : null);
            // The feast's cook cooks as much at the stove (the food theirs, the stove and the hands the colony's).
            PlayCook.Hold(ctx.count);
        }
    }

    // ===================== The undertakers =====================

    /// <summary>
    /// A day or more after a fight, with four or more of the enemy dead still lying out, a burial order of a friendly
    /// people offers to take them away: graves dug outside the wall for silver, or the dead loaded on their beast and
    /// carried off, the raiders' gear with them, for nothing.
    /// </summary>
    public class HappeningWorker_Undertakers : HappeningWorker_Callers
    {
        public static List<Thing> Dead(Map map) =>
            PlayHaul.EnemyDead(map, 40).Where(c => c is Corpse k && k.Age >= CallerMath.DeadDays * GenDate.TicksPerDay).ToList();

        /// <summary>A beast of their caravans to carry the dead: the people's own pack animal, else one of the usual ones that can stand the weather.</summary>
        private static PawnKindDef BeastKind(Faction f, Map map)
        {
            var kind = Couriers.AnimalKind(f, map);
            if (kind != null) return kind;
            foreach (var n in new[] { "Muffalo", "Dromedary", "Horse", "Donkey", "Yak", "Alpaca" })
            {
                var k = DefDatabase<PawnKindDef>.GetNamedSilentFail(n);
                if (k?.race?.race != null && k.RaceProps.packAnimal && map.mapTemperature.SeasonAndOutdoorTemperatureAcceptableFor(k.race, 0f)) return k;
            }
            return null;
        }

        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            base.CanHappen(map, ext) && !MapFacts.ThreatOn(map) && map.dangerWatcher.DangerRating == StoryDanger.None
            && Now - map.storyState.LastThreatBigTick > GenDate.TicksPerDay && Dead(map).Count >= CallerMath.MinDead;

        /// <summary>
        /// The people who come: any friendly one, a people of the calling the event prefers likeliest (the Quiet Bell's
        /// sextons, by patch: Patches/Rimfolk_PeoplesD.xml); the Bell's own event comes only from the Bell
        /// (Peoples/PeoplesD.cs).
        /// </summary>
        protected virtual Faction People(HappeningExtension ext) => Arrivals.Friendly(null, Peoples.Weigh(ext.prefers));

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = People(ext);
            var dead = Dead(ctx.map);
            if (ctx.faction == null || dead.Count < CallerMath.MinDead) return false;
            MakeGroup(ctx, ext, ext.count.RandomInRange, ext.ages, null, false);
            if (ctx.stranger == null) return false;
            var kind = BeastKind(ctx.faction, ctx.map);
            if (kind != null)
            {
                try
                {
                    ctx.pet = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, ctx.faction, PawnGenerationContext.NonPlayer, forceGenerateNewPawn: true));
                    ctx.pet?.inventory?.DestroyAll();
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("undertakers' beast", e);
                    ctx.pet = null;
                }
            }
            ctx.variant = dead.Count;
            ctx.count = CallerMath.GraveFee(Mathf.Min(dead.Count, CallerMath.MaxGraves));
            ctx.a = Colonists(ctx.map).Where(p => !p.DevelopmentalStage.Juvenile()).RandomElementWithFallback();
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            var all = ctx.group.Prepend(ctx.stranger).ToList();
            if (ctx.pet != null) all.Add(ctx.pet);
            return Arrivals.Visit(all, ctx.map, ctx.faction, Gate(ctx.map), ext.stayHours.RandomInRange) != null;
        }

        protected override void Abort(HappeningContext ctx)
        {
            if (ctx.pet != null && !ctx.pet.Spawned && !ctx.pet.Discarded) Find.WorldPawns.PassToWorld(ctx.pet, PawnDiscardDecideMode.Discard);
            ctx.pet = null;
            base.Abort(ctx);
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            if (ctx.pet == null) tags.Add("nobeast");
        }

        protected override string Extra(HappeningContext ctx)
        {
            float gear = 0f;
            foreach (var c in Dead(ctx.map))
            {
                var x = (c as Corpse)?.InnerPawn;
                if (x?.apparel != null) gear += x.apparel.WornApparel.Sum(a => a.MarketValue);
                gear += Callers.GearBy(c).Sum(w => w.MarketValue * w.stackCount);
            }
            return "CC_Caller_Undertakers_Extra".Translate(ctx.variant, Mathf.RoundToInt(gear / 10f) * 10).Resolve();
        }
    }

    /// <summary>The undertakers dig a grave each outside the wall (beyond the gate, off the home area) and lay the enemy dead in them (Callers).</summary>
    public class DilemmaEffect_UndertakersBury : DilemmaEffect
    {
        private static List<IntVec3> Plot(DilemmaContext ctx, out Rot4 rot)
        {
            rot = Rot4.North;
            var map = ctx?.map;
            if (map == null || ctx.stranger == null) return new List<IntVec3>();
            int n = Mathf.Min(HappeningWorker_Undertakers.Dead(map).Count, CallerMath.MaxGraves);
            return n <= 0 ? new List<IntVec3>() : PlayBurial.Plot(map, ctx.stranger, n, true, out rot);
        }

        public override string Unavailable(DilemmaContext ctx) => Plot(ctx, out _).Count == 0 ? "CC_Caller_NoGraveRoom".Translate().Resolve() : null;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            int n = Plot(ctx, out _).Count;
            if (n > 0) sheet.Play("CC_Impact_Caller_Graves".Translate(n).Resolve(), "CC_Impact_Caller_GravesBrief".Translate().Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            var plots = Plot(ctx, out var rot);
            if (hc == null || plots.Count == 0) return;
            var dead = HappeningWorker_Undertakers.Dead(ctx.map);
            var t = Callers.Graves(ctx.map, hc.group.Prepend(hc.stranger).ToList(), dead, plots, rot, null, hc.incident?.defName);
            if (t != null)
            {
                t.ctxRef = ctx;
                return;
            }
            if (PlayOuts.On) return;
            // "Answers play out on the map" off: the graves and the burying are had at once, as answers used to.
            for (int i = 0; i < plots.Count && i < dead.Count; i++)
            {
                var grave = Callers.DigNow(ctx.map, plots[i], rot);
                var body = dead[i];
                if (grave == null || body == null || !body.Spawned) continue;
                body.DeSpawn();
                if (!grave.TryAcceptThing(body, false) && !body.Destroyed) GenPlace.TryPlaceThing(body, grave.Position, ctx.map, ThingPlaceMode.Near);
            }
        }
    }

    /// <summary>The undertakers load the enemy dead on their beast and take them away, the raiders' gear with them (Callers).</summary>
    public class DilemmaEffect_UndertakersTake : DilemmaEffect
    {
        public override string Unavailable(DilemmaContext ctx) =>
            ctx?.pet == null || ctx.pet.Dead || !ctx.pet.Spawned ? "CC_Caller_NoBeast".Translate().Resolve() : null;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (ctx?.map == null || ctx.pet == null) return;
            var dead = HappeningWorker_Undertakers.Dead(ctx.map);
            sheet.Play("CC_Impact_Caller_TakeDead".Translate(dead.Count, ctx.pet.Named("OTHER")).Resolve(), "CC_Impact_Caller_TakeDeadBrief".Translate().Resolve());
            int gear = 0;
            foreach (var c in dead)
                gear += ((c as Corpse)?.InnerPawn?.apparel?.WornApparel.Count ?? 0) + Callers.GearBy(c).Count;
            if (gear > 0) sheet.Goods(-1, "CC_Caller_RaidersGear".Translate().Resolve(), gear, "raiders gear");
        }

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            if (hc?.stranger == null || ctx.pet == null) return;
            var dead = HappeningWorker_Undertakers.Dead(ctx.map);
            var t = Callers.Load(ctx.map, hc.group.Prepend(hc.stranger).ToList(), dead, ctx.pet, null, hc.incident?.defName);
            if (t != null)
            {
                t.ctxRef = ctx;
                return;
            }
            if (PlayOuts.On) return;
            // "Answers play out on the map" off: the dead and their gear are gone at once, as answers used to.
            foreach (var body in dead)
            {
                foreach (var w in Callers.GearBy(body)) w.Destroy();
                if (!body.Destroyed) body.Destroy();
            }
            DilemmaEffect_SendAway.Send(hc.stranger);
        }
    }

    // ===================== Kin come for a captive =====================

    /// <summary>
    /// Family of a prisoner held five days or more (or, with Ideology, of a slave kept a month) comes to the gate with
    /// silver for their freedom: take it, let them go for nothing, let the family talk with them at their door, or turn
    /// the family away.
    /// </summary>
    public class HappeningWorker_KinForCaptive : HappeningWorker_Callers
    {
        public static IEnumerable<Pawn> Captives(Map map)
        {
            foreach (var p in map.mapPawns.PrisonersOfColonySpawned)
                if (Fits(p) && p.guest != null && p.guest.ExclusiveInteractionMode != PrisonerInteractionModeDefOf.Release
                    && p.guest.ExclusiveInteractionMode != PrisonerInteractionModeDefOf.Execution
                    && (p.records?.GetValue(RecordDefOf.TimeAsPrisoner) ?? 0f) >= CallerMath.PrisonerDays * GenDate.TicksPerDay)
                    yield return p;
            if (!ModsConfig.IdeologyActive) yield break;
            foreach (var p in map.mapPawns.SlavesOfColonySpawned)
                if (Fits(p) && (p.records?.GetValue(RecordDefOf.TimeAsColonistOrColonyAnimal) ?? 0f) >= CallerMath.SlaveDays * GenDate.TicksPerDay)
                    yield return p;
        }

        private static bool Fits(Pawn p) =>
            p != null && !p.Dead && p.RaceProps.Humanlike && !p.Downed && !p.DevelopmentalStage.Juvenile() && p.Name is NameTriple && !QuestUtility.IsReservedByQuestOrQuestBeingGenerated(p);

        /// <summary>The people the kin come from: the captive's own if they're at peace with the colony, else a friendly people.</summary>
        private static Faction KinPeople(Pawn p)
        {
            var f = p.guest?.SlaveFaction ?? p.Faction;
            if (f != null && !f.IsPlayer && Arrivals.Usable(f) && f.def.humanlikeFaction && Arrivals.PersonKind(f) != null) return f;
            return Arrivals.Friendly();
        }

        protected override bool CanHappen(Map map, HappeningExtension ext) => base.CanHappen(map, ext) && Captives(map).Any();

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var captive = Captives(ctx.map).RandomElementWithFallback();
            if (captive == null) return false;
            ctx.faction = KinPeople(captive);
            var kind = Arrivals.PersonKind(ctx.faction);
            if (ctx.faction == null || kind == null) return false;
            string last = (captive.Name as NameTriple)?.Last;
            float age = captive.ageTracker.AgeBiologicalYearsFloat;
            var kin = new List<Pawn>();
            try
            {
                // Their family, made theirs in the game's relations: their parents if they have none yet; else (someone
                // older) a grown son or daughter, or a brother or sister of their own parents; each with their partner.
                var mother = captive.GetMother();
                var father = captive.GetFather();
                string mode = age > 40f ? "child" : mother == null && father == null ? "parent" : "sibling";
                var range = mode == "parent" ? new FloatRange(Mathf.Min(age + 20f, 72f), Mathf.Min(age + 30f, 80f))
                    : mode == "child" ? new FloatRange(Mathf.Max(18f, age - 30f), Mathf.Max(19f, age - 20f))
                    : new FloatRange(Mathf.Max(16f, age - 6f), Mathf.Max(17f, age + 6f));
                var first = Arrivals.Make(kind, ctx.faction, range, null, last);
                switch (mode)
                {
                    case "parent":
                        captive.relations?.AddDirectRelation(PawnRelationDefOf.Parent, first);
                        break;
                    case "child":
                        first.relations?.AddDirectRelation(PawnRelationDefOf.Parent, captive);
                        break;
                    default:
                        foreach (var parent in new[] { mother, father })
                            if (parent != null) first.relations?.AddDirectRelation(PawnRelationDefOf.Parent, parent);
                        break;
                }
                kin.Add(first);
                var second = Arrivals.Make(kind, ctx.faction, range, first.gender == Gender.Male ? Gender.Female : Gender.Male, mode == "parent" ? last : null);
                first.relations?.AddDirectRelation(PawnRelationDefOf.Spouse, second);
                if (mode == "parent") captive.relations?.AddDirectRelation(PawnRelationDefOf.Parent, second);
                kin.Add(second);
                bool she = first.gender == Gender.Female;
                string key = mode == "parent" ? (she ? "CC_Caller_Kin_Mother" : "CC_Caller_Kin_Father")
                    : mode == "child" ? (she ? "CC_Caller_Kin_Daughter" : "CC_Caller_Kin_Son")
                    : (she ? "CC_Caller_Kin_Sister" : "CC_Caller_Kin_Brother");
                ctx.relation = key.Translate().Resolve();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("captive's kin", e);
                foreach (var p in kin) Arrivals.Discard(p);
                return false;
            }
            ctx.stranger = kin[0];
            ctx.group = kin.Skip(1).ToList();
            ctx.place = captive;
            ctx.tale = captive.LabelShort;
            ctx.count = CallerMath.Ransom(captive.MarketValue);
            ctx.note = captive.IsSlaveOfColony ? "slave" : "prisoner";
            ctx.a = Colonists(ctx.map).Where(p => !p.DevelopmentalStage.Juvenile() && !p.WorkTypeIsDisabled(WorkTypeDefOf.Warden))
                .OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0).FirstOrDefault() ?? Colonists(ctx.map).FirstOrDefault();
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(ctx.group.Prepend(ctx.stranger).ToList(), ctx.map, ctx.faction, Gate(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            tags.Add(ctx.note == "slave" ? "slave" : "prisoner");
        }
    }

    /// <summary>The captive ({PLACE}) let go to their kin: the wardens walk a prisoner out with the game's own release, a slave is set free, and they leave with the kin (Callers).</summary>
    public class DilemmaEffect_KinFree : DilemmaEffect
    {
        private static Pawn Captive(DilemmaContext ctx) => (ctx as HappeningContext)?.place as Pawn;

        public override string Unavailable(DilemmaContext ctx)
        {
            var c = Captive(ctx);
            return c == null || c.Dead || !(c.IsPrisonerOfColony || c.IsSlaveOfColony) ? "CC_Caller_CaptiveGone".Translate().Resolve() : null;
        }

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var c = Captive(ctx);
            if (c == null || ctx.stranger == null) return;
            sheet.Add(ImpactKind.People, -1, "CC_Impact_Caller_Free".Translate(c.Named("PAWN"), ctx.stranger.Named("OTHER")).Resolve(), "CC_Impact_Caller_FreeBrief".Translate(c.Named("PAWN")).Resolve(), "free|" + c.thingIDNumber);
        }

        public override void Apply(DilemmaContext ctx)
        {
            var c = Captive(ctx);
            var x = ctx.stranger;
            if (c == null || c.Dead || x == null) return;
            var t = Callers.Free(ctx.map, x, c, null);
            if (t != null)
            {
                t.ctxRef = ctx;
                return;
            }
            // Nothing played out: let go there and then, as answers always had it.
            if (c.IsPrisonerOfColony) GenGuest.PrisonerRelease(c);
            else if (c.IsSlaveOfColony) GenGuest.SlaveRelease(c);
        }
    }

    /// <summary>One of the kin is shown to the captive's door and talks with them an hour; their family's words soften them, and the kin go home (Callers).</summary>
    public class DilemmaEffect_KinTalk : DilemmaEffect
    {
        private static Pawn Captive(DilemmaContext ctx) => (ctx as HappeningContext)?.place as Pawn;

        public override string Unavailable(DilemmaContext ctx)
        {
            var c = Captive(ctx);
            if (c == null || c.Dead || !(c.IsPrisonerOfColony || c.IsSlaveOfColony)) return "CC_Caller_CaptiveGone".Translate().Resolve();
            return c.IsPrisonerOfColony ? null : "CC_Caller_OnlyPrisoner".Translate().Resolve();
        }

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var c = Captive(ctx);
            if (c == null || ctx.stranger == null || !c.IsPrisonerOfColony) return;
            sheet.Play("CC_Impact_Caller_Talk".Translate(ctx.stranger.Named("OTHER"), c.Named("PAWN")).Resolve(), "CC_Impact_Caller_TalkBrief".Translate().Resolve());
            sheet.Add(ImpactKind.Other, 1, "CC_Impact_Caller_TalkSoftens".Translate(c.Named("PAWN"), CallerMath.TalkResistance, CallerMath.TalkWill).Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            var c = Captive(ctx);
            var x = ctx.stranger;
            if (c?.guest == null || c.Dead || x == null || !c.IsPrisonerOfColony) return;
            var t = Callers.Talk(ctx.map, x, c, null, (ctx as HappeningContext)?.incident?.defName);
            if (t != null)
            {
                t.ctxRef = ctx;
                return;
            }
            c.guest.resistance = Mathf.Max(0f, c.guest.resistance - CallerMath.TalkResistance);
            c.guest.will = Mathf.Max(0f, c.guest.will - CallerMath.TalkWill);
            DilemmaEffect_SendAway.Send(x);
        }
    }

    /// <summary>The kin turned away at the gate: the prisoner digs in (their resistance rises).</summary>
    public class DilemmaEffect_KinRefused : DilemmaEffect
    {
        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var c = (ctx as HappeningContext)?.place as Pawn;
            if (c != null && c.IsPrisonerOfColony) sheet.Add(ImpactKind.Other, -1, "CC_Impact_Caller_Refused".Translate(c.Named("PAWN"), CallerMath.RefuseResistance).Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            var c = (ctx as HappeningContext)?.place as Pawn;
            if (c?.guest != null && !c.Dead && c.IsPrisonerOfColony) c.guest.resistance += CallerMath.RefuseResistance;
        }
    }

    // ===================== The traveling barber =====================

    /// <summary>A barber sets a stool by the gate (or by the colony's styling station) and offers each colonist a new cut.</summary>
    public class HappeningWorker_Barber : HappeningWorker_Callers
    {
        public static List<Pawn> Clients(Map map) =>
            Colonists(map).Where(p => p.story?.hairDef != null && !p.DevelopmentalStage.Baby() && !p.Downed).ToList();

        /// <summary>Doesn't hold with fuss over looks: an ascetic, a psychopath.</summary>
        private static bool Refuses(Pawn p) =>
            p.story?.traits != null && (p.story.traits.HasTrait(TraitDefOf.Ascetic) || p.story.traits.HasTrait(TraitDefOf.Psychopath));

        /// <summary>Would like a cut today: minds their looks (beautiful, or a look in mind with Ideology), or the day's mood takes them (CallerMath.WantsCut).</summary>
        public static bool Wants(Pawn p, int day)
        {
            if (Refuses(p)) return false;
            var beauty = DefDatabase<TraitDef>.GetNamedSilentFail("Beauty");
            if (beauty != null && (p.story.traits.GetTrait(beauty)?.Degree ?? 0) > 0) return true;
            if (ModsConfig.IdeologyActive && p.style != null && (p.style.LookChangeDesired || p.style.HasUnwantedHairStyle)) return true;
            return CallerMath.WantsCut(p.thingIDNumber, day);
        }

        public static int Day(DilemmaContext ctx) => ctx is HappeningContext hc && int.TryParse(hc.note, out int d) ? d : GenDate.DaysPassed;

        protected override bool CanHappen(Map map, HappeningExtension ext) => base.CanHappen(map, ext) && Clients(map).Count >= 2;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = Arrivals.Friendly();
            if (ctx.faction == null) return false;
            var clients = Clients(ctx.map);
            if (clients.Count < 2) return false;
            MakeGroup(ctx, ext, 1, ext.ages, null, false);
            if (ctx.stranger == null) return false;
            int day = GenDate.DaysPassed;
            ctx.note = day.ToString();
            int price = CallerMath.CutPrice(!Avail.IsPreIndustrial);
            var wanting = clients.Where(p => Wants(p, day)).ToList();
            ctx.count = price * clients.Count;
            ctx.variant = price * wanting.Count;
            ctx.tale = wanting.Count == 0 ? "CC_Caller_Barber_Nobody".Translate().Resolve() : TextUtil.JoinAnd(wanting.Take(4).Select(p => p.LabelShort).ToList());
            ctx.a = clients.Where(p => !p.DevelopmentalStage.Juvenile()).OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0).FirstOrDefault();
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(new List<Pawn> { ctx.stranger }, ctx.map, ctx.faction, Gate(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            if (DefDatabase<ThingDef>.GetNamedSilentFail("StylingStation") is ThingDef st && ctx.map.listerThings.ThingsOfDef(st).Any(b => b.Faction == Faction.OfPlayer)) tags.Add("station");
        }

        protected override string Extra(HappeningContext ctx) =>
            "CC_Caller_Barber_Extra".Translate(CallerMath.CutPrice(!Avail.IsPreIndustrial), ctx.tale, ctx.variant).Resolve();
    }

    /// <summary>The barber cuts each colonist's hair in turn at their stool (<see cref="willing"/>: only those who want a cut) (Callers).</summary>
    public class DilemmaEffect_BarberCuts : DilemmaEffect
    {
        public bool willing;

        private List<Pawn> Who(DilemmaContext ctx)
        {
            if (ctx?.map == null) return new List<Pawn>();
            int day = HappeningWorker_Barber.Day(ctx);
            return HappeningWorker_Barber.Clients(ctx.map).Where(p => !willing || HappeningWorker_Barber.Wants(p, day)).ToList();
        }

        public override string Unavailable(DilemmaContext ctx) => Who(ctx).Count == 0 ? "CC_Caller_Barber_NobodyWants".Translate().Resolve() : null;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var who = Who(ctx);
            if (who.Count == 0 || ctx.stranger == null) return;
            string names = who.Count > 3 ? "CC_Impact_Caller_CutCount".Translate(who.Count).Resolve() : TextUtil.JoinAnd(who.Select(p => p.LabelShort).ToList());
            sheet.Play("CC_Impact_Caller_Cut".Translate(ctx.stranger.Named("OTHER"), names).Resolve(), "CC_Impact_Caller_CutBrief".Translate(who.Count).Resolve());
            sheet.Add(ImpactKind.Needs, 1, "CC_Impact_Caller_CutJoy".Translate(names).Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            var who = Who(ctx);
            var x = ctx?.stranger;
            if (who.Count == 0 || x == null) return;
            var t = Callers.Cut(ctx.map, x, who, null, (ctx as HappeningContext)?.incident?.defName);
            if (t != null)
            {
                t.ctxRef = ctx;
                return;
            }
            // "Answers play out on the map" off: the cuts are had at once, as answers used to.
            if (!PlayOuts.On)
                foreach (var p in who)
                    Callers.Restyle(p);
        }
    }

    // ===================== The animal doctor =====================

    /// <summary>
    /// A drovers' animal doctor offers to see to the colony's hurt and sick animals, and to work a young one a while
    /// (when one is hurt or ill, or short of training it's meant to have).
    /// </summary>
    public class HappeningWorker_Vet : HappeningWorker_Callers
    {
        public static List<Pawn> Patients(Map map) =>
            map.mapPawns.SpawnedColonyAnimals.Where(Callers.Patient).OrderByDescending(a => a.health.summaryHealth.SummaryHealthPercent < 0.6f).Take(CallerMath.MaxPatients).ToList();

        public static Pawn Trainee(Map map) =>
            map.mapPawns.SpawnedColonyAnimals.Where(Callers.Trainable).OrderBy(a => a.ageTracker.AgeBiologicalYearsFloat).FirstOrDefault();

        /// <summary>Feed the colony could pay in: hay or kibble, whichever it has more of.</summary>
        private static ThingDef Feed(Map map)
        {
            var opts = new[] { ThingDefOf.Hay, ThingDefOf.Kibble }.Where(d => d != null).ToList();
            return opts.OrderByDescending(d => map.resourceCounter.GetCount(d)).FirstOrDefault();
        }

        protected override bool CanHappen(Map map, HappeningExtension ext) => base.CanHappen(map, ext) && (Patients(map).Count > 0 || Trainee(map) != null);

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            // Any friendly people; a people of the calling the event prefers likeliest (the Circuit of Tamers', by patch).
            ctx.faction = Arrivals.Friendly(null, Peoples.Weigh(ext.prefers));
            if (ctx.faction == null) return false;
            var patients = Patients(ctx.map);
            var young = Trainee(ctx.map);
            if (patients.Count == 0 && young == null) return false;
            MakeGroup(ctx, ext, 1, ext.ages, null, false);
            var x = ctx.stranger;
            if (x == null) return false;
            foreach (var (skill, lo, hi) in new[] { (SkillDefOf.Animals, 10, 16), (SkillDefOf.Medicine, 8, 13) })
            {
                var r = x.skills?.GetSkill(skill);
                if (r != null && !r.TotallyDisabled && r.Level < lo) r.Level = Rand.RangeInclusive(lo, hi);
            }
            // Their own medicine for the colony's animals, what their people use.
            try
            {
                var med = ThingMaker.MakeThing(Avail.IsPreIndustrial ? ThingDefOf.MedicineHerbal : ThingDefOf.MedicineIndustrial);
                med.stackCount = Mathf.Clamp(patients.Count + 1, 2, 6);
                x.inventory?.innerContainer.TryAdd(med);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("animal doctor's medicine", e);
            }
            ctx.pet = young;
            ctx.count = CallerMath.VetFee(patients.Count, young != null);
            ctx.thing = Feed(ctx.map);
            ctx.variant = ctx.thing == null ? 0 : CallerMath.FeedFor(ctx.count, ctx.thing.BaseMarketValue);
            ctx.tale = patients.Count == 0 ? "CC_Caller_Vet_NoPatients".Translate().Resolve()
                : TextUtil.JoinAnd(patients.Take(3).Select(a => a.LabelShort).ToList()) + (patients.Count > 3 ? "CC_Caller_Vet_More".Translate(patients.Count - 3).Resolve() : "");
            ctx.a = Colonists(ctx.map).Where(p => !p.DevelopmentalStage.Juvenile() && !p.WorkTypeIsDisabled(WorkTypeDefOf.Handling))
                .OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Animals)?.Level ?? 0).FirstOrDefault() ?? Colonists(ctx.map).FirstOrDefault();
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(new List<Pawn> { ctx.stranger }, ctx.map, ctx.faction, null, ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            tags.Add(Patients(ctx.map).Count > 0 ? "hurt" : "nohurt");
            if (ctx.pet != null) tags.Add("young");
        }
    }

    /// <summary>
    /// The animal doctor goes from animal to animal tending the colony's hurt and sick ones with their own medicine, then
    /// (with <see cref="train"/>) works the young one a while, one step of its training given; with <see cref="lodge"/>,
    /// they sleep the night in a warm corner of the colony's (Callers, VisitorServices.Lodge).
    /// </summary>
    public class DilemmaEffect_VetRounds : DilemmaEffect
    {
        public bool train = true;
        public bool lodge;

        /// <summary>Nothing for them to do on these terms (no animal hurt or sick, and no training in them).</summary>
        public override string Unavailable(DilemmaContext ctx) =>
            ctx?.map != null && HappeningWorker_Vet.Patients(ctx.map).Count == 0 && !(train && Callers.Trainable(ctx.pet)) ? "CC_Caller_Vet_Nothing".Translate().Resolve() : null;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (ctx?.map == null || ctx.stranger == null) return;
            var patients = HappeningWorker_Vet.Patients(ctx.map);
            var young = train && Callers.Trainable(ctx.pet) ? ctx.pet : null;
            if (patients.Count > 0) sheet.Play("CC_Impact_Caller_Tend".Translate(ctx.stranger.Named("OTHER"), patients.Count).Resolve(), "CC_Impact_Caller_TendBrief".Translate(patients.Count).Resolve());
            if (young != null) sheet.Add(ImpactKind.Skill, 1, "CC_Impact_Caller_Train".Translate(young.Named("PET"), young.training.NextTrainableToTrain()?.label ?? "").Resolve(), "CC_Impact_Caller_TrainBrief".Translate().Resolve());
            if (lodge) sheet.Play("CC_Impact_Caller_Lodge".Translate(ctx.stranger.Named("OTHER")).Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            var x = ctx?.stranger;
            if (x == null || ctx.map == null) return;
            if (lodge) VisitorServices.Lodge(ctx.map, x, false);
            var patients = HappeningWorker_Vet.Patients(ctx.map);
            var young = train && Callers.Trainable(ctx.pet) ? ctx.pet : null;
            var t = Callers.Vet(ctx.map, x, patients, young, null, (ctx as HappeningContext)?.incident?.defName);
            if (t != null)
            {
                t.ctxRef = ctx;
                return;
            }
            if (PlayOuts.On) return;
            // "Answers play out on the map" off: the tending and the training are had at once, as answers used to.
            foreach (var a in patients) Callers.TendNow(x, a);
            if (young != null) Callers.TrainNow(x, young);
        }
    }
}
