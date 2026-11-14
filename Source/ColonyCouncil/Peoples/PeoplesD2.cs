using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    // Release D's touchpoints, continued (Peoples/PeoplesD.cs has the Wardens and the Quiet Bell): the Circuit of Tamers'
    // bout, egg-trader and ranger, and the Wheel's fixer, scrap-runners and wreck. Each only from a people of its calling at
    // peace with the colony, each played out on the map.

    // ===================== The Circuit of Tamers: a friendly bout =====================

    /// <summary>
    /// A roaming tamer of the Circuit comes with a companion (with PokéWorld a Pokémon of the Fighting type, else one of the
    /// land's own trainable beasts) and challenges the colony's best hand with beasts to a friendly bout, by the
    /// Fighting-type spar's rules (Integrations/PokeWorldTypes.cs): the trainer steps up to the companion and they trade
    /// practice swings in turn, the trainer learning from every one; one bout in 25 the companion's blow lands, a bruise of
    /// 3 to 6 through the harm setting's cap and scale and the shared budget (HarmKinds.TamerBout). Who wins goes by the
    /// trainer's way with beasts and their own hands (PeoplesDMath.WinChance): beaten fairly, the tamer ties a token on
    /// and hands over an egg of the Circuit's hatchery, the Circuit's highest honour; winning, they are gracious about it.
    /// The bout waits as one of the events' visits until it is fought, eight hours at most.
    /// </summary>
    public class HappeningWorker_TamerBout : HappeningWorker_Visitors
    {
        public const string Kind = "tamerBout";

        public static JobDef BoutJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_TamerBout");

        private static bool Fit(PawnKindDef k)
        {
            var rp = k?.RaceProps;
            return rp != null && rp.Animal && !rp.Insect && !rp.Dryad && !rp.IsMechanoid && !rp.IsAnomalyEntity && rp.baseBodySize >= 0.5f && rp.baseBodySize <= 2.5f
                   && rp.trainability != null && rp.trainability != TrainabilityDefOf.None && !PokeWorldLink.IsPokemon(k);
        }

        /// <summary>The tamer's companion: a Pokémon of the Fighting type (PokéWorld, never a legendary), else one of the land's own trainable beasts, else a hound or a wolf.</summary>
        public static PawnKindDef Companion(Map map)
        {
            if (PokeWorldLink.Active)
            {
                var mons = DefDatabase<PawnKindDef>.AllDefsListForReading.Where(k => k?.race != null && PokeWorldLink.IsPokemon(k) && PokeWorldLink.TypesOf(k.race).Contains("Fighting")
                                                                                  && !PokeWorldLegends.IsLegendary(k.race) && k.RaceProps.baseBodySize <= 2.5f).ToList();
                if (mons.Count > 0) return mons.RandomElement();
            }
            var biome = map?.Biome;
            if (biome != null)
            {
                var land = BiomeAnimals.Of(biome).Where(Fit).Where(k => map.mapTemperature.SeasonAndOutdoorTemperatureAcceptableFor(k.race, 0f)).ToList();
                if (land.Count > 0) return land.RandomElementByWeight(k => Mathf.Max(0.05f, BiomeAnimals.Commonality(biome, k)));
            }
            foreach (var n in new[] { "Husky", "Wolf_Timber", "LabradorRetriever" })
            {
                var k = DefDatabase<PawnKindDef>.GetNamedSilentFail(n);
                if (k != null) return k;
            }
            return null;
        }

        /// <summary>The colony's best hand with beasts who can take a bout: grown, up, not unable to fight, not hurting or ill.</summary>
        public static Pawn Trainer(Map map) =>
            PeoplesD.Best(map, SkillDefOf.Animals, p => !p.WorkTagIsDisabled(WorkTags.Violent) && p.skills?.GetSkill(SkillDefOf.Melee)?.TotallyDisabled == false
                                                       && p.health.hediffSet.PainTotal < 0.1f && !HealthAIUtility.ShouldSeekMedicalRest(p));

        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            PeoplesD.Friendly(PeoplesMath.Tamers) != null && map.dangerWatcher.DangerRating == StoryDanger.None && Trainer(map) != null && Companion(map) != null;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Tamers);
            var kind = Companion(ctx.map);
            if (ctx.faction == null || kind == null) return false;
            MakeGroup(ctx, ext, 1, ext.ages, null, false);
            if (ctx.stranger == null) return false;
            StorytellerEvents.Skilled(ctx.stranger, SkillDefOf.Animals, new IntRange(10, 16));
            try
            {
                ctx.pet = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, ctx.faction, PawnGenerationContext.NonPlayer, forceGenerateNewPawn: true,
                    developmentalStages: DevelopmentalStage.Adult));
                ctx.pet?.inventory?.DestroyAll();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples D: a tamer's companion", e);
                ctx.pet = null;
            }
            if (ctx.pet == null) return false;
            ctx.beast = ctx.pet.LabelShort;
            ctx.a = Trainer(ctx.map);
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(new List<Pawn> { ctx.stranger, ctx.pet }, ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void Abort(HappeningContext ctx)
        {
            if (ctx.pet != null && !ctx.pet.Spawned && !ctx.pet.Discarded) Find.WorldPawns.PassToWorld(ctx.pet, PawnDiscardDecideMode.Discard);
            ctx.pet = null;
            base.Abort(ctx);
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            if (ctx.pet != null && PokeWorldLink.IsPokemon(ctx.pet)) tags.Add("pokemon");
        }

        /// <summary>The bout begins: the challenge said, the trainer sent to the companion, the bout's wait kept as one of the events' visits.</summary>
        public static void Begin(HappeningContext ctx)
        {
            if (ctx?.map == null || ctx.a == null || ctx.pet == null) return;
            PeoplesD.Say(ctx.stranger, ctx.Ext, ctx, "challenge");
            var v = CouncilGameComponent.Instance?.happenings?.AddVisit(Kind, ctx.incident, ctx.map, ctx.stranger, ctx.a, null, Find.TickManager.TicksGame + 8 * GenDate.TicksPerHour);
            if (v == null) return;
            v.place = ctx.pet;
            // The bout itself: the trainer walks up to the companion and they spar; with the play-outs off, its end at once.
            if (!PlayOuts.On || !StartBout(v)) Done(ctx.a, ctx.pet);
        }

        /// <summary>The trainer goes to the companion and the bout begins; false when it can't.</summary>
        private static bool StartBout(Visit v)
        {
            var trainer = v.a;
            var mon = v.place as Pawn;
            if (BoutJob == null || trainer == null || mon == null || !trainer.Spawned || !mon.Spawned || trainer.Downed || mon.Downed || trainer.InMentalState) return false;
            var job = JobMaker.MakeJob(BoutJob, mon);
            job.count = Rand.RangeInclusive(PeoplesDMath.BoutMinTicks, PeoplesDMath.BoutMaxTicks);
            return trainer.jobs.TryTakeOrderedJob(job, JobTag.Misc);
        }

        /// <summary>The bout waits, each hour: fought, or the tamer or the companion gone, or the eight hours passed; a trainer pulled away is sent again.</summary>
        public static bool Hour(Visit v, CouncilGameComponent comp, int now)
        {
            if (v.done || now > v.until) return false;
            var mon = v.place as Pawn;
            if (mon == null || mon.Dead || !mon.Spawned || v.x == null || !v.x.Spawned) return false;
            if (v.a?.CurJobDef != BoutJob && PlayOuts.On && v.map.dangerWatcher.DangerRating == StoryDanger.None) StartBout(v);
            return true;
        }

        /// <summary>The bout is fought (JobDriver_TamerBout's end): who won, the blow that may have landed, and what the tamer says and gives.</summary>
        public static void Done(Pawn trainer, Pawn mon)
        {
            var visits = CouncilGameComponent.Instance?.happenings?.visits;
            var v = visits?.FirstOrDefault(x => x.kind == Kind && !x.done && x.a == trainer && x.place == mon);
            if (v == null || trainer == null || mon == null || trainer.Dead) return;
            v.done = true;
            var map = trainer.MapHeld;
            var tamer = v.x;
            var f = tamer?.Faction ?? mon.Faction;
            int animals = trainer.skills?.GetSkill(SkillDefOf.Animals)?.Level ?? 0, melee = trainer.skills?.GetSkill(SkillDefOf.Melee)?.Level ?? 0;
            bool won = Rand.Chance(PeoplesDMath.WinChance(animals, melee));
            trainer.skills?.Learn(SkillDefOf.Animals, PeoplesDMath.BoutAnimalsXp);
            var ext = v.incident?.GetModExtension<HappeningExtension>();
            // together: the bout's own end, the tamer beside the ring.
            if (tamer != null && tamer.Spawned && !tamer.Dead && ext?.lines != null)
            {
                string line = Flavor.PickText(ext.lines.Where(l => l.StartsWith(won ? "[won]" : "[lost]")).Select(l => l.Substring(l.IndexOf(']') + 1).TrimStart()).ToList());
                if (!line.NullOrEmpty()) Speech.Say(tamer, line);
            }
            if (f != null && !f.IsPlayer && f.HasGoodwill) WorldSituationWorker.Goodwill(f, won ? PeoplesDMath.WinGoodwill : PeoplesDMath.LoseGoodwill);
            if (won)
            {
                var thought = DefDatabase<ThoughtDef>.GetNamedSilentFail("Rimfolk_Evt_BeatTamer");
                if (thought != null) CouncilGameComponent.GiveThought(trainer, thought, null);
                // The token tied on, and an egg of the Circuit's hatchery handed over.
                var egg = HappeningWorker_EggTrade.Egg(map, null) ?? HappeningWorker_EggTrade.Egg(null, null);
                if (egg != null && map != null) Couriers.Hand(egg, 1, map, trainer.Spawned ? trainer.Position : IntVec3.Invalid, f, tamer);
            }
            if (map != null)
                Tell.Message(NewsKind.People, Importance.Notable, map,
                    (won ? "CC_PeoplesD_BoutWon" : "CC_PeoplesD_BoutLost").Translate(trainer.Named("PAWN"), (tamer?.LabelShort ?? "?").Named("TAMER"), mon.LabelShort.Named("PET")).Resolve(),
                    new LookTargets(trainer), MessageTypeDefOf.NeutralEvent);
            // One bout in 25 the companion's blow lands: the Fighting-type spar's rule, through the harm setting and the budget.
            if (!Harm.Roll(trainer, HarmKinds.TamerBout, PeoplesDMath.BoutBlowChance, HurtClass.Bout)) return;
            var wound = Harm.Injure(trainer, HurtKind.Bruise, HurtWhere.Any, PeoplesDMath.BoutLow, PeoplesDMath.BoutHigh, HarmKinds.TamerBout, HurtClass.Bout);
            if (wound == null) return;
            if (mon.Spawned) mon.Drawer?.Notify_MeleeAttackOn(trainer);
            Tell.Message(NewsKind.People, Importance.Important, map,
                "CC_PeoplesD_BoutBlow".Translate(trainer.Named("PAWN"), mon.LabelShort.Named("PET"), (wound.Part?.Label ?? "").Named("PART")).Resolve(), new LookTargets(trainer),
                MessageTypeDefOf.NegativeHealthEvent);
        }
    }

    /// <summary>The tamer's challenge taken: the colony's trainer and the companion in a friendly bout (HappeningWorker_TamerBout.Begin).</summary>
    public class DilemmaEffect_TamerBout : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override string Unavailable(DilemmaContext ctx) =>
            ctx?.a == null || ctx.a.Dead || ctx.a.Downed || ctx.pet == null || ctx.pet.Dead || !ctx.pet.Spawned ? "CC_PeoplesD_NoBout".Translate().Resolve() : null;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (ctx?.a == null || ctx.pet == null) return;
            sheet.Play("CC_PeoplesD_Impact_Bout".Translate(ctx.a.LabelShort, ctx.pet.LabelShort).Resolve(), "CC_PeoplesD_Impact_BoutBrief".Translate().Resolve());
            // One bout in 25 the companion's blow lands (the harm setting's cap and scale, the shared budget).
            if (Harm.On) sheet.Add(ImpactKind.Health, -1, "CC_PeoplesD_Impact_BoutRisk".Translate(ctx.a.LabelShort).Resolve(), "CC_PeoplesD_Impact_BoutRiskBrief".Translate().Resolve(), "boutrisk");
        }

        public override void Apply(DilemmaContext ctx) => HappeningWorker_TamerBout.Begin(ctx as HappeningContext);
    }

    /// <summary>
    /// The trainer's side of a tamer's bout: up to the companion, practice swings each way in turn, then who won. Its count
    /// of swings isn't kept with the game: a bout loaded halfway starts its swings again.
    /// </summary>
    public class JobDriver_TamerBout : JobDriver
    {
        private int swings;
        private int sinceSwing;

        private Pawn Mon => job.targetA.Thing as Pawn;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() => Mon == null || Mon.Dead || Mon.Downed || Mon.InMentalState || pawn.Map == null || pawn.Map.dangerWatcher.DangerRating != StoryDanger.None);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            var bout = ToilMaker.MakeToil("TamerBout");
            bout.initAction = () =>
            {
                var mon = Mon;
                if (mon == null || PokeWorldTypes.StayJob == null) return;
                try
                {
                    var stay = JobMaker.MakeJob(PokeWorldTypes.StayJob, pawn);
                    stay.count = job.count + 200;
                    mon.jobs.StartJob(stay, JobCondition.InterruptForced, null, false, true);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("peoples D: a bout's companion", e);
                }
            };
            bout.tickIntervalAction = delta =>
            {
                var mon = Mon;
                if (mon == null || !pawn.Position.InHorDistOf(mon.Position, 2.5f))
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }
                pawn.rotationTracker.FaceTarget(mon);
                sinceSwing += delta;
                if (sinceSwing < PeoplesDMath.BoutSwingTicks) return;
                sinceSwing = 0;
                swings++;
                // The game's lunge, each way in turn; melee practice for the trainer, dust kicked up now and then.
                if (swings % 2 == 1)
                {
                    pawn.Drawer?.Notify_MeleeAttackOn(mon);
                    pawn.skills?.Learn(SkillDefOf.Melee, PeoplesDMath.BoutXpSwing);
                }
                else mon.Drawer?.Notify_MeleeAttackOn(pawn);
                if (swings % 3 == 0) FleckMaker.ThrowDustPuff(mon.Position, pawn.Map, 0.5f);
            };
            bout.defaultCompleteMode = ToilCompleteMode.Delay;
            bout.defaultDuration = Mathf.Max(600, job.count);
            bout.handlingFacing = true;
            bout.socialMode = RandomSocialMode.Off;
            yield return bout;
            var done = ToilMaker.MakeToil("TamerBoutDone");
            done.initAction = () => HappeningWorker_TamerBout.Done(pawn, Mon);
            done.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return done;
        }
    }

    // ===================== The Circuit's egg-trader =====================

    /// <summary>
    /// An egg-trader of the Circuit comes with eggs of its hatchery: fertile eggs of a kind the colony doesn't keep (with
    /// PokéWorld a Pokémon's, where it has them). Swapped for one of the colony's own fertile eggs (carried out to them),
    /// or bought for half again their worth; they are set down by the gate (DilemmaEffect_TamerEgg). {TALE} is what they
    /// bring, {COUNT} its price.
    /// </summary>
    public class HappeningWorker_EggTrade : HappeningWorker_Visitors
    {
        private static ThingCategoryDef fertile;

        private static ThingCategoryDef Fertile => fertile ??= DefDatabase<ThingCategoryDef>.GetNamedSilentFail("EggsFertilized");

        private static ThingDef HatchesFrom(ThingDef egg) => egg?.GetCompProperties<CompProperties_Hatcher>()?.hatcherPawn?.race;

        private static bool FertileEgg(ThingDef d) => d != null && Fertile != null && d.thingCategories != null && d.thingCategories.Contains(Fertile) && HatchesFrom(d) != null;

        /// <summary>
        /// An egg of the Circuit's hatchery: of a kind the colony doesn't keep (<paramref name="map"/>'s animals) and whose
        /// young can stand the weather there; with PokéWorld, a Pokémon's first. Null when none.
        /// </summary>
        public static ThingDef Egg(Map map, ThingDef not)
        {
            var kept = map == null ? new HashSet<ThingDef>() : new HashSet<ThingDef>(map.mapPawns.SpawnedColonyAnimals.Select(a => a.def));
            var all = DefDatabase<ThingDef>.AllDefsListForReading.Where(d => FertileEgg(d) && d != not && !kept.Contains(HatchesFrom(d))
                                                                            && (map == null || map.mapTemperature.SeasonAndOutdoorTemperatureAcceptableFor(HatchesFrom(d), 0f))).ToList();
            if (all.Count == 0) return null;
            var mons = all.Where(d => PokeWorldLink.IsPokemon(HatchesFrom(d)) && !PokeWorldLegends.IsLegendary(HatchesFrom(d))).ToList();
            return (mons.Count > 0 ? mons : all.Where(d => !PokeWorldLink.IsPokemon(HatchesFrom(d))).DefaultIfEmpty(all[0])).RandomElement();
        }

        /// <summary>One of the colony's own fertile eggs to swap, the kind it has most of in its stores, or null.</summary>
        public static ThingDef Ours(Map map)
        {
            if (map == null || Fertile == null) return null;
            return map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver).Where(t => FertileEgg(t.def) && t.Spawned && !t.IsForbidden(Faction.OfPlayer) && t.IsInAnyStorage())
                .GroupBy(t => t.def).OrderByDescending(g => g.Sum(t => t.stackCount)).Select(g => g.Key).FirstOrDefault();
        }

        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            PeoplesD.Friendly(PeoplesMath.Tamers) != null && Egg(map, null) != null && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Tamers);
            if (ctx.faction == null) return false;
            var ours = Ours(ctx.map);
            ctx.thing = Egg(ctx.map, ours);
            if (ctx.thing == null) return false;
            ctx.variant = Rand.RangeInclusive(1, 2);
            ctx.count = PeoplesDMath.EggPrice(ctx.thing.BaseMarketValue, ctx.variant);
            ctx.tale = VarietyUtil.Bundle(ctx.thing, ctx.variant);
            ctx.note = ours?.defName;
            ctx.beast = HatchesFrom(ctx.thing)?.label ?? "";
            ctx.stranger = Arrivals.Make(PeoplesD.TraderKind(ctx.faction), ctx.faction, ext.ages);
            ctx.a = PeoplesD.Best(ctx.map, SkillDefOf.Animals);
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return ctx.stranger != null && ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(new List<Pawn> { ctx.stranger }, ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            if (!ctx.note.NullOrEmpty()) tags.Add("ours");
            if (PokeWorldLink.IsPokemon(HatchesFrom(ctx.thing))) tags.Add("pokemon");
        }

        public override string Unavailable(HappeningContext ctx, int option) =>
            option == 0 && (ctx.note.NullOrEmpty() || PlayCarry.Have(ctx.map, ctx.note, null) < 1) ? "CC_PeoplesD_NoEgg".Translate().Resolve() : null;

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (option <= 1 && !byDefault) PeoplesD.Say(ctx.stranger, Ext, ctx, "egg");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The egg-trader's eggs ({THING}, so many: HappeningContext.variant) set down by the gate; with <see cref="swap"/>, one of the colony's own carried out to them.</summary>
    public class DilemmaEffect_TamerEgg : DilemmaEffect
    {
        public bool swap;

        public override bool ChanceEach => false;

        private static int Count(DilemmaContext ctx) => (ctx as HappeningContext)?.variant ?? 1;

        private static ThingDef Ours(DilemmaContext ctx) => (ctx as HappeningContext)?.note is string n && !n.NullOrEmpty() ? DefDatabase<ThingDef>.GetNamedSilentFail(n) : null;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (ctx?.thing == null) return;
            if (swap && Ours(ctx) != null) Carry(ctx).Describe(ctx, sheet);
            sheet.Goods(1, ctx.thing, Math.Max(1, Count(ctx)));
        }

        private static DilemmaEffect_CarryOut Carry(DilemmaContext ctx) =>
            new DilemmaEffect_CarryOut { what = Ours(ctx)?.defName, count = 1, label = "CC_PeoplesD_EggCarried".Translate().Resolve() };

        public override void Apply(DilemmaContext ctx)
        {
            var map = ctx?.map;
            if (map == null || ctx.thing == null) return;
            if (swap && Ours(ctx) != null) Carry(ctx).Apply(ctx);
            var x = ctx.stranger;
            Couriers.Hand(ctx.thing, Math.Max(1, Count(ctx)), map, x != null && x.Spawned && x.Map == map ? x.Position : IntVec3.Invalid, Couriers.PeopleOf(ctx), x);
        }
    }

    // ===================== The Circuit's ranger brings a stray home =====================

    /// <summary>
    /// One of the colony's animals strayed (went wild, or wandered off the colony's land on its own) within the last few
    /// months and is still alive out in the world, and a ranger of the Circuit, who knows every beast on the region's
    /// roads, walks it home to the gate. The colony takes it back (and may give the ranger something for the trouble), or
    /// lets the ranger keep it. {PET} is the animal, {A} the one who handled it.
    /// </summary>
    public class HappeningWorker_TamerStray : HappeningWorker_Visitors
    {
        /// <summary>The colony's stray: an animal of its that went wild within the stray's days, or one of its own out in the world in no caravan, alive and on no map.</summary>
        public static Pawn Stray(out Pawn handler)
        {
            handler = null;
            int now = Now;
            var world = Find.WorldPawns;
            if (world == null) return null;
            var book = AnimalBook.Instance;
            if (book?.gone != null)
                foreach (var g in book.gone.Where(g => g != null && g.wild && PeoplesDMath.StrayRecent(g.tick, now)).OrderByDescending(g => g.tick))
                {
                    var p = world.AllPawnsAlive.FirstOrDefault(x => x != null && x.thingIDNumber == g.id);
                    if (Fits(p, null))
                    {
                        handler = g.handler;
                        return p;
                    }
                }
            var lost = world.AllPawnsAlive.FirstOrDefault(p => Fits(p, Faction.OfPlayer));
            if (lost != null) handler = lost.playerSettings?.Master;
            return lost;
        }

        private static bool Fits(Pawn p, Faction of) =>
            p != null && !p.Dead && !p.Destroyed && !p.Spawned && p.RaceProps?.Animal == true && p.Faction == of && p.GetCaravan() == null
            && !QuestUtility.IsReservedByQuestOrQuestBeingGenerated(p) && (of != null || p.Name != null);

        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            PeoplesD.Friendly(PeoplesMath.Tamers) != null && Stray(out _) != null && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Tamers);
            ctx.pet = Stray(out var handler);
            if (ctx.faction == null || ctx.pet == null) return false;
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Tamers_Ranger"), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            ctx.a = handler != null && !handler.Dead && handler.Spawned && handler.Map == ctx.map && handler.IsColonist ? handler : PeoplesD.Best(ctx.map, SkillDefOf.Animals);
            ctx.beast = ctx.pet.LabelShort;
            ctx.count = PeoplesDMath.Finder(ctx.pet.MarketValue);
            ctx.note = ctx.pet.Faction == null ? "wild" : null;
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return ctx.a != null;
        }

        /// <summary>The ranger walks in with the stray on a lead (theirs while it walks with them).</summary>
        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            var pet = ctx.pet;
            if (pet == null || pet.Dead || pet.Spawned) return false;
            if (pet.Faction != ctx.faction) pet.SetFaction(ctx.faction);
            if (Arrivals.Visit(new List<Pawn> { ctx.stranger, pet }, ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null) return true;
            if (!pet.Spawned) pet.SetFaction(ctx.note == "wild" ? null : Faction.OfPlayer);
            return false;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            if (ctx.note == "wild") tags.Add("wild");
        }

        public override string Unavailable(HappeningContext ctx, int option) =>
            option <= 1 && (ctx.pet == null || ctx.pet.Dead || !ctx.pet.Spawned) ? "CC_PeoplesD_StrayGone".Translate(ctx.beast ?? "?").Resolve() : null;

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesD.Say(ctx.stranger, Ext, ctx, option <= 1 ? "home" : "keep");
            outcome = base.Resolved(ctx, option, risk, byDefault, outcome);
            // Kept by the ranger: it walks off with them (the answer sent the ranger on; an animal goes as the ranger's).
            var pet = ctx.pet;
            if (option == 2 && pet != null && pet.Spawned && !pet.Dead && pet.Faction != Faction.OfPlayer)
            {
                Lords.Leave(pet);
                LordMaker.MakeNewLord(pet.Faction, new LordJob_ExitMapBest(Pace.Errand, false, true), pet.Map, new List<Pawn> { pet });
            }
            return outcome;
        }

        /// <summary>The event couldn't begin after all: a stray put back as it was in the world.</summary>
        protected override void Abort(HappeningContext ctx)
        {
            var pet = ctx.pet;
            if (pet != null && !pet.Spawned && !pet.Dead && pet.Faction == ctx.faction) pet.SetFaction(ctx.note == "wild" ? null : Faction.OfPlayer);
            ctx.pet = null;
            base.Abort(ctx);
        }
    }

    /// <summary>The stray the ranger walked in is the colony's again, at the gate where it stands; the one who handled it glad of it.</summary>
    public class DilemmaEffect_StrayBack : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override string Unavailable(DilemmaContext ctx) =>
            ctx?.pet == null || ctx.pet.Dead || !ctx.pet.Spawned ? "CC_PeoplesD_StrayGone".Translate((ctx as HappeningContext)?.beast ?? "?").Resolve() : null;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (ctx?.pet == null) return;
            sheet.Play("CC_PeoplesD_Impact_StrayBack".Translate(ctx.pet.LabelShort).Resolve(), "CC_PeoplesD_Impact_StrayBackBrief".Translate().Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            var pet = ctx?.pet;
            if (pet == null || pet.Dead || !pet.Spawned) return;
            try
            {
                Lords.Leave(pet);
                pet.SetFaction(Faction.OfPlayer);
                if (ctx.a != null && !ctx.a.Dead && ctx.a.IsColonist && pet.playerSettings != null && pet.training != null && pet.training.HasLearned(TrainableDefOf.Obedience))
                    pet.playerSettings.Master = ctx.a;
                var thought = DefDatabase<ThoughtDef>.GetNamedSilentFail("Rimfolk_Evt_StrayHome");
                // apart: the one who handled it is glad it is home, wherever they are.
                if (thought != null && ctx.a != null && !ctx.a.Dead) CouncilGameComponent.GiveThought(ctx.a, thought, null);
                Tell.Message(NewsKind.Land, Importance.Notable, pet.Map, "CC_PeoplesD_StrayHome".Translate(pet.Named("PET"), (ctx.stranger?.LabelShort ?? "?").Named("RANGER")).Resolve(),
                    new LookTargets(pet), MessageTypeDefOf.PositiveEvent);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples D: a stray brought home", e);
            }
        }
    }

    // ===================== The Wheel's fixer =====================

    /// <summary>
    /// The travelling tinker's mending (Happenings/Workers_Variety.cs), as the Wheel does it: their fixer comes to the gate
    /// when the colony's clothes and weapons are worn, names a price, and works through them one at a time on the map
    /// (VisitorServices.Mend), or teaches the colony's crafter the Wheel's tricks for less.
    /// </summary>
    public class HappeningWorker_WheelMending : HappeningWorker_Tinker
    {
        protected override Faction People(HappeningExtension ext) => PeoplesD.Friendly(PeoplesMath.Salvagers);

        protected override PawnKindDef Mender(Faction f) => PeoplesD.TraderKind(f);

        protected override bool CanHappen(Map map, HappeningExtension ext) => PeoplesD.Friendly(PeoplesMath.Salvagers) != null && base.CanHappen(map, ext);

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesD.Say(ctx.stranger, Ext, ctx, option == 1 ? "refused" : "mend");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    // ===================== The Wheel's scrap-runners =====================

    /// <summary>
    /// The colony has scrap lying about (slag on its home ground or in its stores, clothes and weapons too far gone in its
    /// stores: PlayCarry.Scrap), and the Wheel's scrap-runners come to buy it: carried out to them at the gate and paid for
    /// in silver ({COUNT}), or traded for a piece the Wheel mended back to new ({THING}, of the colony's own age), handed
    /// over at the gate (DilemmaEffect_WheelRefurbished).
    /// </summary>
    public class HappeningWorker_WheelScrap : HappeningWorker_Visitors
    {
        /// <summary>The scrap there is: how many pieces, and what the broken ones would have been worth whole.</summary>
        public static int Scrap(Map map, out int slag, out float brokenWorth)
        {
            slag = 0;
            brokenWorth = 0f;
            var all = PlayCarry.Stock(map, PlayCarry.Scrap, null).ToList();
            foreach (var t in all)
            {
                if (t.def == ThingDefOf.ChunkSlagSteel) slag++;
                else brokenWorth += t.GetStatValue(StatDefOf.MarketValueIgnoreHp);
            }
            return all.Count;
        }

        /// <summary>A piece the Wheel mended back to new, for the colony's age: a weapon of its means (bows and blades for a tribal or medieval colony, the game's guns after).</summary>
        public static ThingDef Refurbished(int price)
        {
            int age = (int)Ages.Ours;
            var tags = age <= 0 ? new[] { "NeolithicRangedDecent", "NeolithicMeleeAdvanced" } : age == 1 ? new[] { "MedievalMeleeDecent", "MedievalMeleeAdvanced", "NeolithicRangedDecent" }
                : age == 2 ? new[] { "SimpleGun", "Gun" } : new[] { "Gun", "SpacerGun" };
            float most = Mathf.Max(150f, price * 1.5f);
            var all = DefDatabase<ThingDef>.AllDefsListForReading.Where(d => d.IsWeapon && d.equipmentType == EquipmentType.Primary && !d.weaponTags.NullOrEmpty() && d.weaponTags.Any(tags.Contains)
                                                                            && d.BaseMarketValue <= most && d.tradeability != Tradeability.None && d.GetCompProperties<CompProperties_Biocodable>() == null).ToList();
            return all.Count == 0 ? null : all.RandomElementByWeight(d => Mathf.Max(0.1f, d.generateCommonality));
        }

        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            PeoplesD.Friendly(PeoplesMath.Salvagers) != null && Scrap(map, out _, out _) >= PeoplesDMath.ScrapMin && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Salvagers);
            if (ctx.faction == null) return false;
            int n = Scrap(ctx.map, out int slag, out float worth);
            if (n < PeoplesDMath.ScrapMin) return false;
            ctx.variant = Math.Min(n, PeoplesDMath.ScrapMax);
            float share = n > 0 ? ctx.variant / (float)n : 1f;
            ctx.count = PeoplesDMath.ScrapPrice(Mathf.RoundToInt(slag * share), worth * share);
            ctx.thing = Refurbished(ctx.count);
            MakeGroup(ctx, ext, Rand.RangeInclusive(2, 3), ext.ages, null, false);
            ctx.a = PeoplesD.Able(ctx.map).OrderBy(p => p.Position.DistanceToSquared(PeoplesD.GateSpot(ctx.map) ?? p.Position)).FirstOrDefault();
            ctx.tale = slag > 0 ? "CC_PeoplesD_ScrapSlag".Translate(slag, n - slag).Resolve() : "CC_PeoplesD_ScrapBroken".Translate(n).Resolve();
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return ctx.stranger != null && ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(ctx.group.Prepend(ctx.stranger).ToList(), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            if (ctx.thing == null) tags.Add("nopiece");
        }

        public override string Unavailable(HappeningContext ctx, int option)
        {
            if (option == 1 && ctx.thing == null) return "CC_PeoplesD_NoPiece".Translate().Resolve();
            return option <= 1 && PlayCarry.Have(ctx.map, PlayCarry.Scrap, null) < Math.Min(ctx.variant, PeoplesDMath.ScrapMin) ? "CC_PeoplesD_ScrapGone".Translate().Resolve() : null;
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            // Sold: the runners pay at the gate (Couriers/: the silver carried in); the scrap goes out by the answer's errand.
            if (option == 0 && !byDefault) Couriers.PaySilver(ctx.map, VarietyUtil.Near(ctx.a, ctx.map), ctx.count, Couriers.PeopleOf(ctx), ctx.stranger);
            if (!byDefault) PeoplesD.Say(ctx.stranger, Ext, ctx, option == 2 ? "refused" : "scrap");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>A piece the Wheel mended back to new ({THING}), of good quality, handed over at the gate by their runner.</summary>
    public class DilemmaEffect_WheelRefurbished : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (ctx?.thing != null) sheet.Goods(1, ctx.thing, 1);
        }

        public override void Apply(DilemmaContext ctx)
        {
            var map = ctx?.map;
            var def = ctx?.thing;
            if (map == null || def == null) return;
            try
            {
                var stuff = def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null;
                var t = ThingMaker.MakeThing(def, stuff);
                t.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Good, ArtGenerationContext.Outsider);
                var x = ctx.stranger;
                Couriers.Loot(new List<Thing> { t }, map, x != null && x.Spawned && x.Map == map ? x.Position : IntVec3.Invalid, Couriers.PeopleOf(ctx), x);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples D: the Wheel's mended piece", e);
            }
        }
    }

    // ===================== A wreck brings the Wheel at a run =====================

    /// <summary>
    /// A mechanoid's wreck lies near the colony (Happenings/Dangers.cs finds it), and the Wheel's runners come as fast as
    /// their beasts will carry them: they would strip it where it lies and split the parts, half for the colony, or buy it
    /// whole ({COUNT} silver, three fifths of what its parts are worth) and take everything. Either way two or three of them
    /// work at it for a few hours with sparks flying (DilemmaEffect_WheelStrip), and what is the colony's is left where
    /// it lay.
    /// </summary>
    public class HappeningWorker_WheelWreck : HappeningWorker_Visitors
    {
        /// <summary>What the wreck's parts are worth (its butcher products), for the price.</summary>
        public static float PartsWorth(Corpse c)
        {
            if (c?.InnerPawn?.def?.butcherProducts == null) return 0f;
            return c.InnerPawn.def.butcherProducts.Sum(p => p.thingDef == null ? 0f : p.thingDef.BaseMarketValue * p.count);
        }

        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            PeoplesD.Friendly(PeoplesMath.Salvagers) != null && HappeningWorker_TwitchingScrap.Scrap(map) != null && !MapFacts.ThreatOn(map)
            && map.dangerWatcher.DangerRating == StoryDanger.None && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Salvagers);
            var wreck = HappeningWorker_TwitchingScrap.Scrap(ctx.map);
            if (ctx.faction == null || wreck == null) return false;
            ctx.place = wreck;
            ctx.beast = wreck.InnerPawn?.KindLabel ?? wreck.LabelShort;
            ctx.count = PeoplesDMath.WreckPrice(PartsWorth(wreck));
            MakeGroup(ctx, ext, Rand.RangeInclusive(2, 3), ext.ages, null, false);
            ctx.a = PeoplesD.Best(ctx.map, SkillDefOf.Crafting);
            ctx.placeName = StorytellerEvents.Direction(ctx.map, wreck.Position);
            return ctx.stranger != null && ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(ctx.group.Prepend(ctx.stranger).ToList(), ctx.map, ctx.faction, ctx.place?.Position, ext.stayHours.RandomInRange) != null;

        public override string Unavailable(HappeningContext ctx, int option) =>
            option <= 1 && (ctx.place == null || ctx.place.Destroyed || !ctx.place.Spawned) ? "CC_PeoplesD_WreckGone".Translate().Resolve() : null;

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            // Sold whole: the runners pay at the gate (Couriers/); the stripping goes by the answer's effect.
            if (option == 1 && !byDefault) Couriers.PaySilver(ctx.map, VarietyUtil.Near(ctx.a, ctx.map), ctx.count, Couriers.PeopleOf(ctx), ctx.stranger);
            if (!byDefault) PeoplesD.Say(ctx.stranger, Ext, ctx, option == 2 ? "refused" : "strip");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>
    /// The Wheel's runners strip the wreck where it lies: one of them works at it for three hours with sparks flying, then
    /// the colony's share (half of each part, <see cref="share"/>; none when sold whole) is left where it lay and the rest
    /// goes into their packs (WheelWreck.Stripped). With the play-outs off, or the runners gone, at once.
    /// </summary>
    public class DilemmaEffect_WheelStrip : DilemmaEffect
    {
        public bool share = true;

        public override bool ChanceEach => false;

        public override string Unavailable(DilemmaContext ctx) =>
            (ctx as HappeningContext)?.place is Corpse c && !c.Destroyed && c.Spawned ? null : "CC_PeoplesD_WreckGone".Translate().Resolve();

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (!((ctx as HappeningContext)?.place is Corpse c)) return;
            sheet.Play((share ? "CC_PeoplesD_Impact_StripShare" : "CC_PeoplesD_Impact_StripSold").Translate(c.InnerPawn?.KindLabel ?? c.LabelShort).Resolve(),
                "CC_PeoplesD_Impact_StripBrief".Translate().Resolve());
            if (!share) return;
            foreach (var p in c.InnerPawn?.def?.butcherProducts ?? new List<ThingDefCountClass>())
                if (p?.thingDef != null && PeoplesDMath.Half(p.count) > 0) sheet.Goods(1, p.thingDef, PeoplesDMath.Half(p.count));
        }

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            if (!(hc?.place is Corpse wreck) || wreck.Destroyed || !wreck.Spawned) return;
            WheelWreck.Shares[wreck.thingIDNumber] = share;
            var runners = hc.group.Prepend(hc.stranger).Where(p => p != null && p.Spawned && !p.Dead && !p.Downed && p.Map == wreck.Map).ToList();
            if (PlayOuts.On && runners.Count > 0 && PeoplesD.StripJob != null)
            {
                PeoplesD.Gather(runners, hc.faction ?? runners[0].Faction, wreck.Position, 5f);
                var lead = runners.OrderBy(p => p.Position.DistanceToSquared(wreck.Position)).First();
                var job = JobMaker.MakeJob(PeoplesD.StripJob, wreck);
                job.count = PeoplesDMath.StripTicks;
                lead.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
                return;
            }
            var worker = runners.FirstOrDefault() ?? hc.stranger;
            if (worker != null) WheelWreck.Stripped(worker, wreck);
        }
    }

    /// <summary>The wreck taken apart (DilemmaEffect_WheelStrip): the colony's share left where it lay, the rest into the runner's pack.</summary>
    public static class WheelWreck
    {
        /// <summary>Whether each wreck being stripped is shared (by its id): unsaved, the game's job is too.</summary>
        public static readonly Dictionary<int, bool> Shares = new Dictionary<int, bool>();

        public static void Stripped(Pawn runner, Corpse wreck)
        {
            if (runner == null || wreck == null || wreck.Destroyed) return;
            bool share = !Shares.TryGetValue(wreck.thingIDNumber, out bool s) || s;
            Shares.Remove(wreck.thingIDNumber);
            string label = wreck.InnerPawn?.KindLabel ?? wreck.LabelShort;
            var map = wreck.MapHeld;
            var at = wreck.PositionHeld;
            if (!wreck.Spawned)
            {
                wreck.Destroy();
                return;
            }
            string kept = PeoplesD.Divide(runner, wreck, t => share ? PeoplesDMath.Half(t.stackCount) : 0);
            if (map == null) return;
            Tell.Message(NewsKind.World, Importance.Notable, map,
                (share ? "CC_PeoplesD_Stripped" : "CC_PeoplesD_StrippedSold").Translate(runner.Named("PAWN"), label.Named("WRECK"),
                    (kept.NullOrEmpty() ? "CC_PeoplesD_Nothing".Translate().Resolve() : kept).Named("KEPT")).Resolve(),
                new LookTargets(at, map), MessageTypeDefOf.PositiveEvent);
        }
    }
}
