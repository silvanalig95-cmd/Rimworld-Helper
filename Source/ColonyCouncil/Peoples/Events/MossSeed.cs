using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    // ===================== 51. A Gauranlen seed (the Mossborn) =====================

    /// <summary>
    /// The Mossborn's seed: once their ledger has warmed to the colony (their goodwill 30 or more, no more trees felled than
    /// planted this season: PeopleEventsMath.LedgerWarm), a seed-carrier plants a Gauranlen seed (Ideology: the game's
    /// seed, planted by the game's own rule, the tree connected to the colonist named) where the colony chooses, with that
    /// colonist beside her; without Ideology she sows a healroot grove instead. The price is a promise, a seedling for every
    /// tree felled, which their ledger watches at each season's reckoning (MossLedger.Reckon, MossPromise).
    /// </summary>
    public static class MossSeed
    {
        public const string GiftKind = "seed";

        public static ThingDef SeedDef => ModsConfig.IdeologyActive ? DefDatabase<ThingDef>.GetNamedSilentFail("GauranlenSeed") : null;

        public static ThingDef Healroot => DefDatabase<ThingDef>.GetNamedSilentFail("Plant_Healroot");

        /// <summary>A Gauranlen tree's seed, else (no Ideology) a healroot grove.</summary>
        public static bool Tree => SeedDef?.GetCompProperties<CompProperties_Plantable>()?.plantDefToSpawn != null;

        /// <summary>The ledger is warm with this people: their goodwill, and no more felled than planted this season.</summary>
        public static bool Warm(Faction f) => f != null && f.HasGoodwill && PeopleEventsMath.LedgerWarm(f.PlayerGoodwill, MossLedger.Felled, MossLedger.Planted);

        public static Faction Friendly() => Arrivals.Friendly(f => PeoplesD.Theirs(f, PeoplesMath.Grovekeepers) && Warm(f));

        /// <summary>The one the ledger names: someone of the colony who plants (Growing), connected to no tree yet.</summary>
        public static IEnumerable<Pawn> Takers(Map map) =>
            PeopleEvents.Takers(map, GiftKind).Where(p => p.skills?.GetSkill(SkillDefOf.Plants)?.TotallyDisabled == false && (p.connections == null || p.connections.ConnectedThings.Count == 0));

        public static Pawn Pick(Map map) =>
            Takers(map).OrderByDescending(p => (p.skills.GetSkill(SkillDefOf.Plants).Level + 1) * (p.skills.GetSkill(SkillDefOf.Plants).passion != Passion.None ? 2 : 1)).FirstOrDefault();

        private static Thing seedCheck;

        /// <summary>The game's rule for planting the seed at a cell (CompPlantable.CanPlantAt), or healroot's for the grove.</summary>
        public static AcceptanceReport CanPlant(Map map, IntVec3 c)
        {
            if (map == null || !c.InBounds(map) || c.Fogged(map)) return false;
            if (Tree)
            {
                seedCheck ??= ThingMaker.MakeThing(SeedDef);
                return seedCheck.TryGetComp<CompPlantable>()?.CanPlantAt(c, map) ?? false;
            }
            var heal = Healroot;
            return heal != null && heal.CanEverPlantAt(c, map, out _, true, false);
        }

        /// <summary>The tree's own reach for buildings that weaken its connection (its comp's radius), or five.</summary>
        private static float Reach => SeedDef?.GetCompProperties<CompProperties_Plantable>()?.plantDefToSpawn?.GetCompProperties<CompProperties_TreeConnection>()?.radiusToBuildingForConnectionStrengthLoss ?? 5f;

        /// <summary>Where the seed-carrier would plant it herself: near the colony, nothing built within the tree's reach, the nearest such cell to the one named.</summary>
        public static IntVec3 HerSpot(Map map, Pawn near)
        {
            if (map == null || near == null) return IntVec3.Invalid;
            var from = CampSite.ColonyCenter(map);
            if (!from.IsValid) from = near.Position;
            foreach (var c in GenRadial.RadialCellsAround(from, 45f, true).Where(c => c.InBounds(map) && !c.InHorDistOf(from, 10f)))
            {
                if (!CanPlant(map, c).Accepted || c.Roofed(map) || !near.CanReach(c, PathEndMode.Touch, Danger.Some)) continue;
                if (Tree && map.listerArtificialBuildingsForMeditation.GetForCell(c, Reach).Any()) continue;
                return c;
            }
            return IntVec3.Invalid;
        }

        /// <summary>
        /// The colony chooses where (the game's targeting, with the seed's own rule shown as it is aimed); cancelled, or with
        /// nobody to aim, she chooses herself. Then the planting begins.
        /// </summary>
        public static void Choose(HappeningContext ctx, bool colony)
        {
            if (ctx?.map == null) return;
            if (!colony || Find.Targeter == null)
            {
                Plant(ctx, HerSpot(ctx.map, ctx.b));
                return;
            }
            bool picked = false;
            try
            {
                var parms = new TargetingParameters { canTargetLocations = true, canTargetPawns = false, canTargetBuildings = false, canTargetItems = false, mapObjectTargetsMustBeAutoAttackable = false };
                Find.Targeter.BeginTargeting(parms, t =>
                {
                    if (!CanPlant(ctx.map, t.Cell).Accepted) return;
                    picked = true;
                    Plant(ctx, t.Cell);
                }, t =>
                {
                    if (CanPlant(ctx.map, t.Cell).Accepted) GenDraw.DrawTargetHighlight(t);
                }, t => CanPlant(ctx.map, t.Cell).Accepted, null, () =>
                {
                    if (!picked) Plant(ctx, HerSpot(ctx.map, ctx.b));
                }, SeedDef?.uiIcon ?? Healroot?.uiIcon, false);
                // direct: the answer to the player's own click, asking them where to plant it.
                Messages.Message("CC_PEv_SeedChoose".Translate().Resolve(), MessageTypeDefOf.NeutralEvent, false);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events: choosing the seed's place", e);
                if (!picked) Plant(ctx, HerSpot(ctx.map, ctx.b));
            }
        }

        /// <summary>The planting: the seed-carrier and the one named walk to the spot, she plants it with them beside her (PeopleWorkOn, Rimfolk_PEv_Seed).</summary>
        public static void Plant(HappeningContext ctx, IntVec3 cell)
        {
            var x = ctx?.stranger;
            var job = DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv_Seed");
            if (ctx?.map == null || x == null || ctx.b == null || job == null) return;
            if (!cell.IsValid)
            {
                Tell.Message(NewsKind.Land, Importance.Notable, ctx.map, "CC_PEv_SeedNoSpot".Translate(x.LabelShort.Named("X")).Resolve(), LookTargets.Invalid, MessageTypeDefOf.NeutralEvent);
                PeoplesG.Home(PeoplesG.Party(ctx));
                return;
            }
            Promise();
            PeopleWorkOn.Begin(ctx, x, ctx.b, job, cell, Tree ? "tree" : "grove", 0, PeopleEventsMath.PlantTicks, 8f);
        }

        /// <summary>The colony's promise, made when the seed goes in: watched from now at every reckoning.</summary>
        public static void Promise()
        {
            var b = PeopleEventsBook.Instance;
            if (b != null && b.promiseSince < 0) b.promiseSince = Find.TickManager.TicksGame;
        }

        /// <summary>
        /// The seed in the ground (the game's own planting of it, then the tree connected to the one named), or the healroot
        /// grove sown round the spot; told.
        /// </summary>
        public static void Planted(Pawn carrier, Pawn on, Visit v)
        {
            var map = v?.map ?? on?.MapHeld;
            var cell = v?.cell ?? IntVec3.Invalid;
            if (map == null || on == null || on.Dead || !cell.IsValid) return;
            string what = null;
            try
            {
                if (Tree && CanPlant(map, cell).Accepted)
                {
                    var seed = ThingMaker.MakeThing(SeedDef);
                    var comp = seed.TryGetComp<CompPlantable>();
                    comp.DoPlant(on, cell, map);
                    var tree = cell.GetPlant(map);
                    tree?.TryGetComp<CompTreeConnection>()?.ConnectToPawn(on, 0.6f);
                    what = "tree";
                }
                else if (Healroot != null)
                {
                    int n = 0;
                    foreach (var c in GenRadial.RadialCellsAround(cell, 1.5f, true))
                    {
                        if (n >= PeopleEventsMath.Grove || !c.InBounds(map) || !Healroot.CanEverPlantAt(c, map, out _, true, false)) continue;
                        if (c.GetPlant(map) is Plant old && old.def != Healroot) old.Destroy();
                        var plant = (Plant)ThingMaker.MakeThing(Healroot);
                        plant.Growth = 0.05f;
                        plant.sown = true;
                        if (GenPlace.TryPlaceThing(plant, c, map, ThingPlaceMode.Direct)) n++;
                    }
                    if (n > 0) what = "grove";
                }
                on.skills?.Learn(SkillDefOf.Plants, 400f);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events: the seed planted", e);
            }
            if (what == null) return;
            PeopleEventsBook.Gave(GiftKind, on);
            FleckMaker.ThrowDustPuff(cell, map, 1f);
            PeoplesG.SayLine(carrier, v.incident, what == "tree" ? "planted" : "sown");
            Tell.Message(NewsKind.Land, Importance.Notable, map,
                (what == "tree" ? "CC_PEv_SeedPlanted" : "CC_PEv_GroveSown").Translate(on.Named("PAWN"), (carrier?.LabelShort ?? "?").Named("CARRIER")).Resolve(),
                new LookTargets(cell, map), MessageTypeDefOf.PositiveEvent);
            CouncilGameComponent.Instance?.AddChronicle(new ChronicleEntry(ChronicleKind.World,
                (what == "tree" ? "CC_PEv_SeedChronicle" : "CC_PEv_GroveChronicle").Translate(on.Named("PAWN"), (carrier?.Faction?.Name ?? "?").Named("FACTION")).Resolve(), on));
        }
    }

    /// <summary>
    /// The colony's promise to the Mossborn at each season's reckoning of their ledger (MossLedger.Reckon): trees felled and
    /// as many planted, kept, and they think the better of it; more felled than planted, broken, a wrong in their memory and
    /// their goodwill down; nothing felled, nothing said.
    /// </summary>
    public static class MossPromise
    {
        public static void Reckoned(int felled, int planted)
        {
            var b = PeopleEventsBook.Instance;
            if (b == null || b.promiseSince < 0) return;
            int verdict = PeopleEventsMath.Promise(felled, planted);
            if (verdict == 0) return;
            if (verdict > 0) b.promisesKept++;
            else b.promisesBroken++;
            var map = Find.AnyPlayerHomeMap;
            foreach (var f in Find.FactionManager.AllFactionsListForReading.Where(f => Peoples.CallingOf(f) == PeoplesMath.Grovekeepers && !f.defeated && f.HasGoodwill && !f.HostileTo(Faction.OfPlayer)))
            {
                try
                {
                    WorldSituationWorker.Goodwill(f, verdict > 0 ? PeopleEventsMath.PromiseKeptGoodwill : PeopleEventsMath.PromiseBrokenGoodwill);
                    if (verdict < 0) FactionMemory.Note(f, "wordBroken", "CC_PEv_PromiseName".Translate().Resolve());
                    Tell.Message(NewsKind.World, Importance.Notable, map,
                        (verdict > 0 ? "CC_PEv_PromiseKept" : "CC_PEv_PromiseBroken").Translate(f.Name.Named("FACTION"), felled.Named("FELLED"), planted.Named("PLANTED")).Resolve(),
                        LookTargets.Invalid, verdict > 0 ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.NegativeEvent);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("peoples' events: the Mossborn's promise reckoned", e);
                }
            }
        }
    }

    /// <summary>
    /// A seed-carrier of the Mossborn comes to the gate with a seed for {B}, the colonist their ledger names: a Gauranlen
    /// seed ({TALE}) to plant where the colony chooses, {B} beside her, the tree theirs; without Ideology a grove of
    /// healroot. The price is a promise: a seedling for every tree the colony fells, which the ledger watches.
    /// </summary>
    public class HappeningWorker_MossSeed : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            MossSeed.Friendly() != null && MossSeed.Takers(map).Any() && (MossSeed.Tree || MossSeed.Healroot != null) && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = MossSeed.Friendly();
            ctx.b = MossSeed.Pick(ctx.map);
            if (ctx.faction == null || ctx.b == null) return false;
            ctx.note = MossSeed.Tree ? "tree" : "grove";
            ctx.tale = MossSeed.Tree ? MossSeed.SeedDef.label : MossSeed.Healroot?.label ?? "?";
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Mossborn_SeedCarrier"), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            ctx.a = ctx.b;
            ctx.count = MossLedger.Planted;
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return true;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            PeopleEvents.AgeTags(tags, ctx.faction);
            tags.Add(ctx.note == "tree" ? "tree" : "grove");
            if (ModsConfig.IsActive("vanillaexpanded.videologye.dryads")) tags.Add("dryads");
        }

        public override string Unavailable(HappeningContext ctx, int option)
        {
            if (option > 1) return null;
            if (ctx.b == null || ctx.b.Dead || !ctx.b.Spawned || ctx.b.Map != ctx.map) return "CC_PEv_NamedGone".Translate().Resolve();
            return null;
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option <= 1 ? "promise" : "thanks");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The seed planted where the colony chooses (<see cref="colony"/>, the game's targeting), or where the seed-carrier does; the promise made.</summary>
    public class DilemmaEffect_MossSeed : DilemmaEffect
    {
        public bool colony;

        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc?.b == null) return;
            sheet.Add(ImpactKind.People, 1, (hc.note == "tree" ? "CC_PEv_Impact_Tree" : "CC_PEv_Impact_Grove").Translate(hc.b.LabelShort).Resolve(),
                (hc.note == "tree" ? "CC_PEv_Impact_TreeBrief" : "CC_PEv_Impact_GroveBrief").Translate().Resolve(), "pevseed");
            sheet.Add(ImpactKind.Goodwill, 0, "CC_PEv_Impact_Promise".Translate(hc.faction?.Name ?? "?").Resolve(), "CC_PEv_Impact_PromiseBrief".Translate().Resolve(), "pevpromise");
            if (PlayOuts.On)
                sheet.Play((colony ? "CC_PEv_Impact_SeedOurs" : "CC_PEv_Impact_SeedHers").Translate(hc.stranger?.LabelShort ?? "?", hc.b.LabelShort).Resolve(),
                    "CC_PEv_Impact_SeedBrief".Translate().Resolve());
        }

        public override void Apply(DilemmaContext ctx) => MossSeed.Choose(ctx as HappeningContext, colony);
    }

    public class MossSeedDone : PeopleWorkDone
    {
        public override void Done(Visit v, Pawn worker, Pawn on) => MossSeed.Planted(worker, on, v);

        public override void GaveUp(Visit v)
        {
            if (v?.map == null) return;
            Tell.Message(NewsKind.Land, Importance.Notable, v.map, "CC_PEv_WorkGivenUp".Translate((v.x?.LabelShort ?? "?").Named("WORKER"), (v.a?.LabelShort ?? "?").Named("PAWN")).Resolve(),
                LookTargets.Invalid, MessageTypeDefOf.NeutralEvent);
        }
    }
}
