using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    // The camp kinds of the player's request of 6 November 2026 (Defs/CampDefs/Rimfolk_CampKinds_Variety3.xml): what
    // their land must have, where they pitch, and their own work by day. Stonecutters come for rock chunks lying clear of
    // the colony and dress them into the game's own blocks; shearers come to a colony with a woolly flock and, taken on,
    // shear it in its pens for one fleece in ten; beast-catchers come for a wild herd and rope one or two of it to lead
    // away; a travelling teacher comes to a colony with children. The wounded of a war come with their wounds
    // (CampKindDef.wounded). The arithmetic is CampLandMath's.

    public static partial class CampLand
    {
        // ---------------- Stone ----------------

        /// <summary>A rock chunk a stonecutter can dress: the game's own blocks come out of it, nobody has marked it, it lies in no stockpile.</summary>
        public static bool Dressable(Thing t, Map map) =>
            t != null && t.Spawned && t.def.category == ThingCategory.Item && t.def.IsWithinCategory(ThingCategoryDefOf.StoneChunks) && !t.def.butcherProducts.NullOrEmpty()
            && map.designationManager.DesignationOn(t) == null && !t.IsInAnyStorage();

        /// <summary>Rock chunks lying clear of the colony, a few hundred of them looked at.</summary>
        public static List<Thing> Chunks(Map map)
        {
            var list = new List<Thing>();
            if (map == null) return list;
            foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver))
                if (Dressable(t, map) && ClearOfColony(map, t.Position)) list.Add(t);
            return list;
        }

        /// <summary>
        /// Where the rock lies thickest, for stonecutters: chunks clear of the colony with a few more within a dozen cells,
        /// the thickest first (<see cref="CampLandMath.MinChunks"/> chunks at least in all).
        /// </summary>
        public static List<IntVec3> StoneTargets(Map map)
        {
            var list = new List<IntVec3>();
            var sample = Chunks(map).InRandomOrder().Take(300).ToList();
            if (!CampLandMath.StoneEnough(sample.Count)) return list;
            var scored = new List<(IntVec3 c, int n)>();
            foreach (var t in sample)
            {
                int near = sample.Count(o => o != t && o.Position.InHorDistOf(t.Position, 12f));
                if (near >= CampLandMath.ScatterAround) scored.Add((t.Position, near));
            }
            foreach (var s in scored.OrderByDescending(s => s.n))
            {
                if (list.Any(c => c.InHorDistOf(s.c, 20f))) continue;
                list.Add(s.c);
                if (list.Count >= 4) break;
            }
            return list;
        }

        // ---------------- The flock ----------------

        /// <summary>One of the colony's grown beasts with its wool half grown or more (the game's own shearing), up and about.</summary>
        public static CompShearable Fleece(Pawn a)
        {
            if (a == null || a.Dead || a.Downed || !a.Spawned || a.Faction != Faction.OfPlayer || !a.RaceProps.Animal || a.InMentalState) return null;
            var s = a.TryGetComp<CompShearable>();
            if (s?.Props?.woolDef == null || !(a.ageTracker?.CurLifeStage?.shearable ?? false)) return null;
            return s.Fullness >= CampLandMath.FleeceReady ? s : null;
        }

        /// <summary>The colony's beasts with wool to shear now.</summary>
        public static List<Pawn> Flock(Map map) =>
            map == null ? new List<Pawn>() : map.mapPawns.SpawnedColonyAnimals.Where(a => Fleece(a) != null).ToList();

        // ---------------- A wild herd ----------------

        /// <summary>
        /// A wild beast beast-catchers could rope: nobody's, of a herding kind that lives on this land and can be tamed, no
        /// predator, grown, sound and calm, never marked by the colony (to hunt or to tame) nor ever tamed before.
        /// </summary>
        public static bool Catchable(Pawn a)
        {
            if (a?.RaceProps == null || !a.Spawned || a.Dead || a.Downed || a.Faction != null || a.InMentalState || !a.RaceProps.Animal) return false;
            var race = a.RaceProps;
            if (race.predator || !race.herdAnimal || race.Insect || race.Dryad || race.IsMechanoid || race.IsAnomalyEntity || race.trainability == null) return false;
            if (a.ageTracker?.CurLifeStage?.reproductive != true || a.Name != null && !a.Name.Numerical) return false;
            if (a.Map.designationManager.DesignationOn(a) != null) return false;
            if (a.GetStatValue(StatDefOf.Wildness) >= CampLandMath.MaxWildness || !TameUtility.CanTame(a)) return false;
            return BiomeAnimals.LivesIn(a.Map.Biome, a.kindDef);
        }

        /// <summary>The wild beasts clear of the colony, by kind, the biggest herd first; only kinds with a herd's worth.</summary>
        public static List<List<Pawn>> WildHerds(Map map)
        {
            if (map == null) return new List<List<Pawn>>();
            return map.mapPawns.AllPawnsSpawned.Where(a => Catchable(a) && ClearOfColony(map, a.Position)).GroupBy(a => a.kindDef)
                .Select(g => g.ToList()).Where(g => CampLandMath.HerdEnough(g.Count)).OrderByDescending(g => g.Count).ToList();
        }

        /// <summary>Where the wild herds graze: the middle of each, the biggest first.</summary>
        public static List<IntVec3> HerdTargets(Map map)
        {
            var list = new List<IntVec3>();
            foreach (var g in WildHerds(map).Take(3))
            {
                int x = Mathf.RoundToInt((float)g.Average(a => a.Position.x)), z = Mathf.RoundToInt((float)g.Average(a => a.Position.z));
                list.Add(new IntVec3(x, 0, z).ClampInsideMap(map));
            }
            return list;
        }
    }

    public partial class Camp
    {
        /// <summary>The stonecutters' sale of their dressed stone, carried to the colony's gate (CampCarry).</summary>
        public const string StoneSale = "stone";

        /// <summary>What the new land kinds did this stay: chunks dressed, beasts shorn and the wool that came of it, beasts caught.</summary>
        public int chunksCut, beastsShorn, woolShorn, woolShare, beastsCaught;
        /// <summary>The shearers were taken on (their offer said yes to); the share in ten carried over from fleece to fleece.</summary>
        public bool shearing;
        public float shearCarry;
        /// <summary>The beasts the beast-catchers roped this stay: theirs now, kept at their picket.</summary>
        public List<Pawn> caught = new List<Pawn>();

        private void ExposeLandMore()
        {
            Scribe_Values.Look(ref chunksCut, "chunksCut");
            Scribe_Values.Look(ref beastsShorn, "beastsShorn");
            Scribe_Values.Look(ref woolShorn, "woolShorn");
            Scribe_Values.Look(ref woolShare, "woolShare");
            Scribe_Values.Look(ref beastsCaught, "beastsCaught");
            Scribe_Values.Look(ref shearing, "shearing");
            Scribe_Values.Look(ref shearCarry, "shearCarry");
            if (Scribe.mode == LoadSaveMode.Saving) caught.RemoveAll(p => p == null || p.Dead || p.Discarded);
            Scribe_Collections.Look(ref caught, "caught", LookMode.Reference);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                caught ??= new List<Pawn>();
                caught.RemoveAll(p => p == null);
            }
        }

        /// <summary>A beast the beast-catchers roped this stay.</summary>
        public bool IsCaught(Pawn a) => a != null && caught.Contains(a);

        // ---------------- Stone ----------------

        [Unsaved] private List<Thing> chunkCache;
        [Unsaved] private int chunkTick = -999999;

        /// <summary>The chunks within reach of their camp they could dress, looked for again every hour.</summary>
        private List<Thing> ChunksNear()
        {
            if (chunkCache != null && Now - chunkTick < GenDate.TicksPerHour)
            {
                chunkCache.RemoveAll(t => t == null || t.Destroyed || !t.Spawned);
                return chunkCache;
            }
            chunkCache = CampLand.Chunks(map).Where(t => t.Position.InHorDistOf(center, CampLand.ClearRadius)).ToList();
            chunkTick = Now;
            return chunkCache;
        }

        /// <summary>The chunks they may dress now: near their camp on their side, or (asked to) the ones nearest the colony first.</summary>
        private List<Thing> CutTargets(Pawn p)
        {
            var list = ChunksNear().Where(t => clearNear || MayWork(t.Position)).ToList();
            var colony = colonyCenter.IsValid ? colonyCenter : map.Center;
            return (clearNear ? list.OrderBy(t => t.Position.DistanceToSquared(colony)) : list.OrderBy(t => t.Position.DistanceToSquared(p.Position))).ToList();
        }

        private Job CutJob(Pawn p)
        {
            if (workToday >= CampLandMath.CutsADay(Adults) || !Rand.Chance(0.6f) || Now < landNextSearch || p.WorkTypeIsDisabled(WorkTypeDefOf.Crafting)) return null;
            foreach (var t in CutTargets(p).Take(12))
            {
                if (Targeted(t) || !p.CanReserveAndReach(t, PathEndMode.Touch, Danger.Some)) continue;
                SayMoment(p, "cut", 0.3f);
                return JobMaker.MakeJob(CampLandDefOf.Rimfolk_CampCut, t);
            }
            landNextSearch = Now + GenDate.TicksPerHour;
            return null;
        }

        /// <summary>A chunk dressed where it lay, as the game's stonecutting makes it: the blocks are theirs, carried to their stash by their own porters.</summary>
        private void Cut(Pawn p, Thing chunk)
        {
            if (chunk == null || !CampLand.Dressable(chunk, map) || !p.Spawned || !p.Position.InHorDistOf(chunk.Position, 2f)) return;
            var products = chunk.def.butcherProducts.Where(x => x?.thingDef != null && x.count > 0).ToList();
            var cell = chunk.Position;
            chunk.Destroy(DestroyMode.Vanish);
            foreach (var x in products)
            {
                var t = ThingMaker.MakeThing(x.thingDef);
                t.stackCount = x.count;
                if (GenPlace.TryPlaceThing(t, cell, map, ThingPlaceMode.Near, out var put) && put != null) Own(put);
            }
            workToday++;
            chunksCut++;
            AddTension(0.4f);
        }

        /// <summary>The most of one kind of their dressed stone at the camp now, a load at most, and how many.</summary>
        public int StoneOffer(out ThingDef def)
        {
            def = null;
            int best = 0;
            foreach (var g in Goods().Where(t => t.def.IsWithinCategory(ThingCategoryDefOf.StoneBlocks)).GroupBy(t => t.def))
            {
                int n = g.Sum(t => t.stackCount);
                if (n <= best) continue;
                best = n;
                def = g.Key;
            }
            return Mathf.Min(best, CampLandMath.StoneLoad);
        }

        public int StoneCost() => VisitorTradeMath.MilkPrice(StoneOffer(out var def), def?.BaseMarketValue ?? 0f);

        private string StoneWords()
        {
            int n = StoneOffer(out var def);
            return n > 0 && def != null
                ? "CC_CampMilk_Offer".Translate(n, def.label, VisitorTradeMath.MilkPrice(n, def.BaseMarketValue)).Resolve()
                : "CC_CampLand_NoStoneWord".Translate().Resolve();
        }

        public string StoneSaleBlocked()
        {
            if (Pending(StoneSale)) return "CC_CampLand_StoneGoing".Translate();
            if (StoneOffer(out _) < CampLandMath.MinStoneSale) return "CC_CampLand_NoStone".Translate();
            if (Porter() == null) return "CC_CampLand_NoPorter".Translate();
            return CanBring ? null : "CC_CampErrand_No_gate".Translate().ToString();
        }

        /// <summary>The colony buys a load of their stone: one of them takes it up from their stash and carries it to the colony's gate, to be paid for there.</summary>
        public bool StartStoneSale()
        {
            int n = StoneOffer(out var def);
            var porter = Porter();
            if (n <= 0 || def == null || porter == null) return false;
            var gate = ErrandCell("gate", null, porter);
            if (!gate.IsValid) return false;
            carries.Add(new CampCarry { by = porter, help = StoneSale, def = def, want = n, to = gate, from = Now, until = Now + GenDate.TicksPerHour * 10 });
            return true;
        }

        /// <summary>Their stone set down by the colony's gate: paid for at its worth.</summary>
        private void StoneBrought(Pawn p, CampCarry c, int n, Thing look)
        {
            int price = VisitorTradeMath.MilkPrice(n, c.def.BaseMarketValue);
            int pay = VisitorTradeMath.PayWith(price, CampAnswers.Silver(map));
            if (pay > 0) CampAnswers.Pay(this, pay);
            string key = pay >= price ? "CC_CampLand_StoneSold" : pay > 0 ? "CC_CampLand_StoneSoldShort" : "CC_CampLand_StoneSoldUnpaid";
            if (pay < price) AddTension(pay > 0 ? 4f : 8f);
            string text = key.Translate(p.Named("X"), n.Named("COUNT"), c.def.label.Named("THING"), pay.Named("SILVER"), price.Named("PRICE"), Label.Named("NAME")).Resolve();
            Note(text, pay >= price ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.NeutralEvent, Importance.Notable, look, true);
        }

        // ---------------- The flock ----------------

        /// <summary>
        /// The shearers offer their work once the camp stands (a letter, as the harvest hands do); turned down, they go on to
        /// the next farm. Taken on, they shear by day; with no fleece left to shear, they finish within a few hours.
        /// </summary>
        private void ShearHourly(int hour)
        {
            if (!offerMade)
            {
                var ask = AskByKey("shear");
                if (ask == null || LetterOpen()) return;
                offerMade = true;
                if (AskBlocked(ask) == null) Request(ask);
                return;
            }
            if (!shearing)
            {
                if (asksMade.Contains("shear") && !LetterOpen("shear")) Leave("declined");
                return;
            }
            if (hour < 8 || hour >= 17) return;
            if (CampLand.Flock(map).Count == 0)
            {
                reapIdleHours++;
                if (reapIdleHours >= 3 && beastsShorn > 0 && untilTick > Now + GenDate.TicksPerHour * 3) untilTick = Now + GenDate.TicksPerHour * 3;
            }
            else reapIdleHours = 0;
        }

        private Job ShearJob(Pawn p)
        {
            if (!shearing || p.WorkTypeIsDisabled(WorkTypeDefOf.Handling) || !Rand.Chance(0.8f)) return null;
            foreach (var beast in CampLand.Flock(map).OrderBy(a => a.Position.DistanceToSquared(p.Position)).Take(10))
            {
                if (Targeted(beast) || !p.CanReserveAndReach(beast, PathEndMode.Touch, Danger.Some)) continue;
                return JobMaker.MakeJob(CampLandDefOf.Rimfolk_CampShearFlock, beast);
            }
            return null;
        }

        /// <summary>
        /// One of the colony's beasts shorn, as the game shears one: its wool left where it was shorn for the colony's
        /// haulers, but for their fleece in ten, which goes in their own pack.
        /// </summary>
        private void Shorn(Pawn p, Pawn beast)
        {
            var comp = CampLand.Fleece(beast);
            if (comp == null || !p.Spawned || p.inventory == null || !p.Position.InHorDistOf(beast.Position, 2.5f)) return;
            var def = comp.Props.woolDef;
            var before = new HashSet<int>(GenRadial.RadialDistinctThingsAround(p.Position, map, 5f, true).Where(x => x.def == def).Select(x => x.thingIDNumber));
            comp.Gathered(p);
            var wool = GenRadial.RadialDistinctThingsAround(p.Position, map, 5f, true).Where(x => x.def == def && !before.Contains(x.thingIDNumber)).ToList();
            int n = wool.Sum(x => x.stackCount);
            int share = CampLandMath.Share(n, CampLandMath.FleeceShare, ref shearCarry), taken = 0;
            foreach (var t in wool)
            {
                int k = Mathf.Min(share - taken, t.stackCount);
                if (k > 0)
                {
                    var part = t.SplitOff(k);
                    if (part.Spawned) part.DeSpawn();
                    p.inventory.TryAddItemNotForSale(part);
                    taken += k;
                }
                if (!t.Destroyed && t.Spawned) t.SetForbidden(false, false);
            }
            workToday++;
            beastsShorn++;
            woolShorn += n;
            woolShare += taken;
            if (beastsShorn % 4 == 1) SayMoment(p, "shear", 0.5f);
        }

        // ---------------- A wild herd ----------------

        [Unsaved] private List<Pawn> herdCache;
        [Unsaved] private int herdTick = -999999;

        /// <summary>The wild herds' beasts within reach of their camp, looked for again every hour.</summary>
        private List<Pawn> HerdNear()
        {
            if (herdCache == null || Now - herdTick >= GenDate.TicksPerHour)
            {
                herdCache = CampLand.WildHerds(map).SelectMany(g => g).Where(a => a.Position.InHorDistOf(center, CampLand.ClearRadius)).ToList();
                herdTick = Now;
            }
            herdCache.RemoveAll(a => !CampLand.Catchable(a));
            return herdCache;
        }

        private Job CatchJob(Pawn p)
        {
            if (caught.Count(a => a != null && !a.Dead) >= CampLandMath.MaxCaught || workToday >= 1 || !Rand.Chance(0.35f) || Now < landNextSearch
                || p.WorkTypeIsDisabled(WorkTypeDefOf.Handling) || p == leader && Here.Count > 2)
                return null;
            foreach (var a in HerdNear().Where(x => MayWork(x.Position)).OrderBy(x => x.Position.DistanceToSquared(p.Position)).Take(6))
            {
                if (Targeted(a) || !p.CanReserveAndReach(a, PathEndMode.Touch, Danger.Some)) continue;
                SayMoment(p, "catch", 0.4f);
                return JobMaker.MakeJob(CampLandDefOf.Rimfolk_CampCatch, a);
            }
            landNextSearch = Now + GenDate.TicksPerHour * 2;
            return null;
        }

        /// <summary>A wild beast roped and gentled where it stood: theirs now, it goes to their picket with their beasts and leaves with them.</summary>
        private void Caught(Pawn p, Pawn beast)
        {
            if (!CampLand.Catchable(beast) || !p.Spawned || !p.Position.InHorDistOf(beast.Position, 2.5f) || faction == null) return;
            try
            {
                beast.SetFaction(faction);
                if (LordAlive && beast.GetLord() == null) lord.AddPawn(beast);
                if (!herd.Contains(beast)) herd.Add(beast);
                if (!caught.Contains(beast)) caught.Add(beast);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("camp beast caught", e);
                return;
            }
            workToday++;
            beastsCaught++;
            AddTension(0.8f);
            Note(Text("CC_CampLand_Caught", p, null, 0, beast.LabelNoCount), MessageTypeDefOf.NeutralEvent, Importance.Notable, beast);
        }

        /// <summary>What the beast-catchers ask for one they caught: by its worth, the one they'd let go first.</summary>
        public int CaughtCost()
        {
            var a = CaughtToSell();
            return a == null ? 0 : CampAnswers.Round10(CampLandMath.CaughtPrice(a.MarketValue));
        }

        public Pawn CaughtToSell() =>
            caught.Where(a => a != null && !a.Dead && a.Spawned && a.Map == map && !a.Downed && herd.Contains(a)).OrderBy(a => a.MarketValue).FirstOrDefault();

        private string CaughtWords()
        {
            var a = CaughtToSell();
            return a == null ? "CC_CampLand_NoCaughtWord".Translate().Resolve()
                : "CC_CampLand_CaughtOffer".Translate(a.LabelNoCount, a.def.label, CaughtCost()).Resolve();
        }

        // ---------------- The wounded ----------------

        /// <summary>
        /// The wounded of a war come with their wounds (CampKindDef.wounded): each of them a bandaged cut, already tended on
        /// the road, and a fresh bruise on another limb that the colony's doctor could see to (the game's own injuries,
        /// shown on the map). Never whoever speaks for them, never a child, and only the first time they come.
        /// </summary>
        private void ComeWounded()
        {
            if (kind == null || kind.wounded.max <= 0 || Familiar) return;
            int n = kind.wounded.RandomInRange;
            var bruise = Harm.InjuryOf(HurtKind.Bruise);
            foreach (var p in members.Where(m => m != null && m != leader && !m.Dead && m.RaceProps.Humanlike && !m.DevelopmentalStage.Juvenile()).InRandomOrder().Take(n))
            {
                try
                {
                    var limbs = p.health.hediffSet.GetNotMissingParts().Where(x => x.depth == BodyPartDepth.Outside && (x.def.defName == "Arm" || x.def.defName == "Leg" || x.def.defName == "Shoulder")).ToList();
                    var first = limbs.RandomElementWithFallback();
                    if (first == null) continue;
                    var cut = HediffMaker.MakeHediff(HediffDefOf.Cut, p, first);
                    cut.Severity = Rand.Range(CampLandMath.WoundMin, CampLandMath.WoundMax);
                    p.health.AddHediff(cut, first);
                    if (cut.TendableNow()) cut.Tended(Rand.Range(0.35f, 0.6f), 1f);
                    var second = limbs.Where(x => x != first).RandomElementWithFallback();
                    if (bruise != null && second != null)
                    {
                        var b = HediffMaker.MakeHediff(bruise, p, second);
                        b.Severity = Rand.Range(CampLandMath.WoundMin * 0.6f, CampLandMath.WoundMax * 0.6f);
                        p.health.AddHediff(b, second);
                    }
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("camp wounded", e);
                }
            }
        }
    }

    // ---------------- Effects the new kinds' asks use ----------------

    /// <summary>The colony buys a load of the stonecutters' dressed stone (their ask): see <see cref="Camp.StartStoneSale"/>.</summary>
    public class DilemmaEffect_CampStoneSale : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var c = CampOnMap.For(ctx);
            if (c == null || c.StoneSaleBlocked() != null) return;
            int n = c.StoneOffer(out var def);
            sheet.Play("CC_Impact_StoneSale".Translate(c.Label).Resolve(), "CC_Impact_StoneSaleBrief".Translate().Resolve());
            sheet.Goods(1, def, n);
            sheet.Silver(-c.StoneCost(), "stone");
        }

        public override string Unavailable(DilemmaContext ctx)
        {
            var c = CampOnMap.For(ctx);
            return c == null ? "CC_CampAsk_NotNow".Translate().ToString() : c.StoneSaleBlocked();
        }

        public override void Apply(DilemmaContext ctx)
        {
            var c = CampOnMap.For(ctx);
            if (c == null || (chance < 1f && !Rand.Chance(chance))) return;
            try
            {
                c.StartStoneSale();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("camp stone sale", e);
            }
        }
    }

    /// <summary>The shearers are taken on (their offer): from now they shear the colony's flock by day, for one fleece in ten.</summary>
    public class DilemmaEffect_CampShearFlock : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var c = CampOnMap.For(ctx);
            if (c?.kind?.work != "shear") return;
            int n = CampLand.Flock(c.map).Count;
            sheet.Play("CC_Impact_ShearFlock".Translate(c.Label, n).Resolve(), "CC_Impact_ShearFlockBrief".Translate().Resolve());
            sheet.Add(ImpactKind.Goods, -1, "CC_Impact_ShearShare".Translate().Resolve(), "CC_Impact_ShearShareBrief".Translate().Resolve());
        }

        public override string Unavailable(DilemmaContext ctx)
        {
            var c = CampOnMap.For(ctx);
            if (c == null) return "CC_CampAsk_NotNow".Translate().ToString();
            return CampLandMath.FleeceEnough(CampLand.Flock(c.map).Count) ? null : "CC_CampLand_NoFleece".Translate().ToString();
        }

        public override void Apply(DilemmaContext ctx)
        {
            var c = CampOnMap.For(ctx);
            if (c?.kind?.work != "shear" || (chance < 1f && !Rand.Chance(chance))) return;
            c.shearing = true;
        }
    }

    /// <summary>
    /// The colony's children at the travelling teacher's side for a lesson at the camp, up to <see cref="count"/> of them:
    /// each child goes to the teacher (the one of the camp best at <see cref="skill"/>) and follows the lesson there.
    /// </summary>
    public class DilemmaEffect_CampChildrenLesson : DilemmaEffect
    {
        public int count = 3;
        public FloatRange duration = new FloatRange(1.5f, 2.5f);
        public SkillDef skill;

        public override bool ChanceEach => false;

        private SkillDef Skill => skill ?? SkillDefOf.Intellectual;

        private Pawn Teacher(Camp c) =>
            c.Here.Where(p => !p.DevelopmentalStage.Juvenile() && !p.Downed && !(p.skills?.GetSkill(Skill)?.TotallyDisabled ?? true))
                .OrderByDescending(p => p.skills.GetSkill(Skill).Level).FirstOrDefault();

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var c = CampOnMap.For(ctx);
            var kids = CampLand.Children(ctx?.map).Take(count).ToList();
            if (c == null || kids.Count == 0 || Teacher(c) == null) return;
            sheet.Play("CC_Impact_ChildrenLesson".Translate(TextUtil.JoinAnd(kids.Select(k => k.LabelShort).ToList()), c.Label, Skill.label).Resolve(), "CC_Impact_ChildrenLessonBrief".Translate().Resolve());
        }

        public override string Unavailable(DilemmaContext ctx)
        {
            var c = CampOnMap.For(ctx);
            if (c == null || Teacher(c) == null) return "CC_CampAsk_NotNow".Translate().ToString();
            return CampLand.Children(ctx?.map).Count == 0 ? "CC_CampLand_NoChildren".Translate().ToString() : null;
        }

        public override void Apply(DilemmaContext ctx)
        {
            var c = CampOnMap.For(ctx);
            var teacher = c == null ? null : Teacher(c);
            if (teacher == null || (chance < 1f && !Rand.Chance(chance))) return;
            foreach (var child in CampLand.Children(ctx.map).Take(count))
            {
                try
                {
                    // together: each child goes over to the teacher at the camp and follows the lesson at their side.
                    var lesson = new DilemmaEffect_Lesson
                    {
                        teacher = PlayWho.X, student = PlayWho.A, skill = Skill, duration = duration, xpPerHour = 180f,
                        label = "CC_CampLand_LessonLabel".Translate(c.Label.Named("NAME")).Resolve()
                    };
                    lesson.Apply(new HappeningContext { map = ctx.map, a = child, stranger = teacher, faction = c.faction });
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("camp children's lesson", e);
                }
            }
        }
    }
}
