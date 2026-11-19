using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using HarmonyLib;
using Verse;

namespace ColonyCouncil
{
    // ===================== 41. An egg from the crater (the Circuit of Tamers) =====================

    /// <summary>
    /// An egg from the crater (item 41, a gift): a tamer of the Circuit brings a rare egg of a beast the land doesn't have,
    /// from the player's beast mods first (Dinosauria, Megafauna, ReGrowth, Alpha Animals, PokéWorld; PeopleEvents5Math.
    /// EggPackages), else the game's own, as Nessa Pell's came from a crater, and asks the colony to hatch it. It is the
    /// colony's to keep warm until it hatches, by the game's own hatching (a cold egg spoils as any does). Whoever of the
    /// colony tends it most (the hours spent near it, or carrying it) is the one the beast bonds to when it hatches; the
    /// keeper named on the card if nobody did. A season after it hatched the tamer comes back to see it grown.
    /// </summary>
    public static class CraterEgg
    {
        public const string GiftKind = "egg";
        public const string EventDef = "Rimfolk_Evt_CraterEgg";
        public const string GrownEvent = "Rimfolk_Evt_CraterEggGrown";

        public static JobDef LookJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv5_EggLook");

        private static int Now => Find.TickManager.TicksGame;

        private static ThingCategoryDef fertile;

        private static ThingCategoryDef Fertile => fertile ??= DefDatabase<ThingCategoryDef>.GetNamedSilentFail("EggsFertilized");

        public static PawnKindDef Hatchling(ThingDef egg) => egg?.GetCompProperties<CompProperties_Hatcher>()?.hatcherPawn;

        private static bool FertileEgg(ThingDef d) => d != null && Fertile != null && d.thingCategories != null && d.thingCategories.Contains(Fertile) && Hatchling(d)?.race != null;

        /// <summary>Of one of the player's beast mods (its package), the order of PeopleEvents5Math.EggPackages; -1: the game's own or another's.</summary>
        public static int ModRank(ThingDef d)
        {
            string pkg = d?.modContentPack?.PackageId?.ToLowerInvariant();
            if (pkg == null) return -1;
            for (int i = 0; i < PeopleEvents5Math.EggPackages.Length; i++)
                if (pkg.StartsWith(PeopleEvents5Math.EggPackages[i], StringComparison.Ordinal)) return i;
            return -1;
        }

        /// <summary>
        /// A rare egg for this land: of a beast that doesn't live wild here and that the colony doesn't keep, whose young can
        /// stand the weather, never an insect's, a dryad's or a legendary Pokémon's; from the player's beast mods first, the
        /// game's own after. Null when there is none.
        /// </summary>
        public static ThingDef Pick(Map map)
        {
            if (map == null) return null;
            var wild = new HashSet<ThingDef>(BiomeAnimals.Of(map.Biome).Select(k => k.race).Where(r => r != null));
            var kept = new HashSet<ThingDef>(map.mapPawns.SpawnedColonyAnimals.Select(a => a.def));
            var all = DefDatabase<ThingDef>.AllDefsListForReading.Where(d =>
            {
                if (!FertileEgg(d)) return false;
                var k = Hatchling(d);
                var rp = k.RaceProps;
                return rp != null && rp.Animal && !rp.Insect && !rp.Dryad && !rp.IsMechanoid && !rp.IsAnomalyEntity && !wild.Contains(k.race) && !kept.Contains(k.race)
                       && !PokeWorldLegends.IsLegendary(k.race) && map.mapTemperature.SeasonAndOutdoorTemperatureAcceptableFor(k.race, 0f);
            }).ToList();
            if (all.Count == 0) return null;
            var mods = all.Where(d => ModRank(d) >= 0).ToList();
            var from = mods.Count > 0 ? mods : all;
            return from.RandomElementByWeight(d => Math.Max(1f, d.BaseMarketValue));
        }

        /// <summary>The egg given: made and handed over at the gate by the tamer, the colony's to hatch (its young the colony's), the record kept.</summary>
        public static void Give(HappeningContext ctx)
        {
            var map = ctx?.map;
            var b = PeopleEvents5Book.Instance;
            var def = ctx?.thing;
            if (map == null || b == null || def == null) return;
            Thing egg;
            try
            {
                egg = ThingMaker.MakeThing(def);
                egg.stackCount = 1;
                var hatcher = egg.TryGetComp<CompHatcher>();
                if (hatcher != null) hatcher.hatcheeFaction = Faction.OfPlayer;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 5: the crater egg", e);
                return;
            }
            var r = b.AddEgg(new EggRecord { faction = ctx.faction, egg = egg, def = def.defName, race = Hatchling(def)?.race?.defName, map = map, keeper = ctx.a, since = Now });
            PeopleEventsBook.Gave(GiftKind, ctx.a);
            var x = ctx.stranger;
            Couriers.Loot(new List<Thing> { egg }, map, x != null && x.Spawned && x.Map == map ? x.Position : PeoplesD.GateSpot(map) ?? map.Center, ctx.faction, x);
            if (r.egg?.Spawned == true) r.lastCell = r.egg.Position;
        }

        /// <summary>Where the egg is now: on the ground, in a store, carried, in someone's pack.</summary>
        private static bool Where(Thing egg, out Map map, out IntVec3 at, out Pawn carrier)
        {
            map = egg?.MapHeld;
            at = egg?.PositionHeld ?? IntVec3.Invalid;
            carrier = egg?.ParentHolder is Pawn_CarryTracker ct ? ct.pawn : egg?.ParentHolder is Pawn_InventoryTracker it ? it.pawn : null;
            return egg != null && !egg.Destroyed && map != null && at.IsValid;
        }

        /// <summary>
        /// The events' hour: each egg tended by whoever is near it or carries it, an hour each; hatched, the beast found by
        /// where the egg lay and bonded to who tended it most; spoiled or lost, told; a season after the hatching, the tamer
        /// comes back to see it grown.
        /// </summary>
        public static void Watch(int now)
        {
            var b = PeopleEvents5Book.Instance;
            if (b == null) return;
            foreach (var r in b.eggs.Where(r => r != null && !r.done).ToList())
            {
                if (r.hatched < 0)
                {
                    if (r.egg != null && Where(r.egg, out var map, out var at, out var carrier))
                    {
                        r.map = map;
                        r.lastCell = at;
                        var hatcher = r.egg.TryGetComp<CompHatcher>();
                        if (hatcher != null && hatcher.TemperatureDamaged)
                        {
                            Lost(r, "CC_PEv5_EggCold");
                            continue;
                        }
                        if (carrier != null && carrier.IsColonist) r.Tend(carrier);
                        foreach (var p in map.mapPawns.FreeColonistsSpawned)
                            if (p != carrier && p.Position.InHorDistOf(at, PeopleEvents5Math.TendCells) && !p.Downed && p.Awake()) r.Tend(p);
                        if (now - r.since > PeopleEvents5Math.EggForgetDays * GenDate.TicksPerDay) r.done = true;
                        continue;
                    }
                    // Still on its way in (a runner carrying it to the gate): nothing to tend yet.
                    if (r.egg != null && !r.egg.Destroyed)
                    {
                        if (now - r.since > PeopleEvents5Math.EggForgetDays * GenDate.TicksPerDay) r.done = true;
                        continue;
                    }
                    // The egg is gone: hatched where it lay, or spoiled, sold or lost.
                    r.egg = null;
                    var beast = Newborn(r);
                    if (beast != null) Hatched(r, beast);
                    else Lost(r, "CC_PEv5_EggLost");
                    continue;
                }
                if (r.beast == null || r.beast.Dead || r.beast.Faction != Faction.OfPlayer)
                {
                    r.done = true;
                    continue;
                }
                if (!r.came && now >= r.back)
                {
                    var home = r.beast.MapHeld;
                    if (home == null || !Homes.IsHome(home) || !r.beast.Spawned || MapFacts.ThreatOn(home) || !Tell.CanAsk) continue;
                    var f = r.faction != null && PeoplesD.Theirs(r.faction, PeoplesMath.Tamers) ? r.faction : PeoplesD.Friendly(PeoplesMath.Tamers);
                    if (f == null)
                    {
                        if (now - r.back > 30 * GenDate.TicksPerDay) r.done = true;
                        continue;
                    }
                    if (PeopleEvents2.Fire(GrownEvent, home, f))
                    {
                        r.came = true;
                        r.done = true;
                    }
                }
            }
        }

        /// <summary>The young that came out of the egg: of its kind, the colony's, at most a few hours old, where it lay.</summary>
        private static Pawn Newborn(EggRecord r)
        {
            if (r.map == null || r.race == null || !r.lastCell.IsValid) return null;
            return r.map.mapPawns.SpawnedColonyAnimals.Where(a => a.def.defName == r.race && a.ageTracker.AgeBiologicalTicks < 3 * GenDate.TicksPerHour
                                                                  && a.Position.InHorDistOf(r.lastCell, 12f)).OrderBy(a => a.Position.DistanceToSquared(r.lastCell)).FirstOrDefault();
        }

        private static void Hatched(EggRecord r, Pawn beast)
        {
            r.beast = beast;
            r.hatched = Now;
            r.back = Now + PeopleEvents5Math.GrownAfterDays * GenDate.TicksPerDay;
            var who = r.MostTended();
            bool bonded = false;
            if (who != null && !who.Dead && beast.relations != null)
                try
                {
                    bonded = RelationsUtility.TryDevelopBondRelation(who, beast, 1f);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("peoples' events 5: the hatchling's bond", e);
                }
            // apart: the news of the hatching, the one who tended it named, wherever they are.
            Tell.Message(NewsKind.Land, Importance.Notable, beast.Map,
                (bonded ? "CC_PEv5_EggHatched" : "CC_PEv5_EggHatchedAlone").Translate(beast.Named("BEAST"), (who?.LabelShort ?? "?").Named("PAWN")).Resolve(), new LookTargets(beast),
                MessageTypeDefOf.PositiveEvent);
        }

        private static void Lost(EggRecord r, string key)
        {
            r.done = true;
            r.lost = true;
            if (r.map != null && Homes.IsHome(r.map))
                Tell.Message(NewsKind.Land, Importance.Notable, r.map, key.Translate((DefDatabase<ThingDef>.GetNamedSilentFail(r.def)?.label ?? "?").Named("THING")).Resolve(), LookTargets.Invalid,
                    MessageTypeDefOf.NegativeEvent);
        }

        /// <summary>Dev mode: the egg here hatches now (its hatching hastened), or the grown beast's tamer is due now.</summary>
        public static string Hasten()
        {
            var b = PeopleEvents5Book.Instance;
            var r = b?.eggs.FirstOrDefault(x => x != null && !x.done);
            if (r == null) return "no crater egg kept.";
            if (r.hatched < 0)
            {
                var h = r.egg?.TryGetComp<CompHatcher>();
                if (h == null) return "the egg is not where it can hatch.";
                AccessTools.Field(typeof(CompHatcher), "gestateProgress")?.SetValue(h, 0.999f);
                return "the egg will hatch within the hour.";
            }
            r.back = Now;
            return "the tamer is due now.";
        }

        /// <summary>The grown beast looked over (the tamer's hour with it): a step of its training, the tamer glad of it.</summary>
        public static void LookedOver(Pawn tamer, Pawn beast)
        {
            if (beast == null || beast.Dead) return;
            // together: the tamer has just spent the hour with the beast at the gate (PeopleWorkOn kept it there).
            Callers.TrainNow(tamer, beast);
            var f = tamer?.Faction;
            if (f != null && f.HasGoodwill && !f.IsPlayer && !f.HostileTo(Faction.OfPlayer)) WorldSituationWorker.Goodwill(f, PeopleEvents5Math.GrownGoodwill, GoodwillWhy.Helped);
            if (tamer != null) PeoplesG.SayLine(tamer, DefDatabase<IncidentDef>.GetNamedSilentFail(GrownEvent), "grown");
            Tell.Message(NewsKind.People, Importance.Notable, beast.MapHeld, "CC_PEv5_EggLookedOver".Translate(beast.Named("BEAST"), (tamer?.LabelShort ?? "?").Named("TAMER")).Resolve(),
                beast.Spawned ? new LookTargets(beast) : LookTargets.Invalid, MessageTypeDefOf.PositiveEvent);
        }
    }

    /// <summary>A tamer of {FACTION} ({X}) at the gate with a rare egg ({THING}, a {BEAST}'s), asking the colony to hatch it; {A} its keeper.</summary>
    public class HappeningWorker_CraterEgg : HappeningWorker_Visitors
    {
        private static Pawn Keeper(Map map) => PeoplesD.Best(map, SkillDefOf.Animals, p => PeopleEvents.MayGive(CraterEgg.GiftKind, p) && !p.DevelopmentalStage.Juvenile());

        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            PeoplesD.Friendly(PeoplesMath.Tamers) != null && PeopleEventsBook.GiftOpen && Keeper(map) != null && CraterEgg.Pick(map) != null
            && !(PeopleEvents5Book.Instance?.eggs.Any(r => r != null && !r.done && r.hatched < 0) ?? false);

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Tamers);
            ctx.a = Keeper(ctx.map);
            ctx.thing = CraterEgg.Pick(ctx.map);
            if (ctx.faction == null || ctx.a == null || ctx.thing == null) return false;
            ctx.beast = CraterEgg.Hatchling(ctx.thing)?.label ?? "?";
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Tamers_Tamer"), ctx.faction, ext.ages);
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return ctx.stranger != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            PeopleEvents.AgeTags(tags, ctx.faction);
            if (PokeWorldLink.IsPokemon(CraterEgg.Hatchling(ctx.thing)?.race)) tags.Add("pokemon");
            if (CraterEgg.ModRank(ctx.thing) >= 0 && !PokeWorldLink.IsPokemon(CraterEgg.Hatchling(ctx.thing)?.race)) tags.Add("far");
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option <= 1 ? "egg" : "later");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The crater egg handed over at the gate, the colony's to hatch (CraterEgg.Give).</summary>
    public class DilemmaEffect_CraterEgg : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (ctx?.thing == null) return;
            sheet.Goods(1, ctx.thing, 1);
            sheet.Add(ImpactKind.Other, 0, "CC_PEv5_Impact_Egg".Translate(ctx.a?.LabelShort ?? "?").Resolve(), "CC_PEv5_Impact_EggBrief".Translate().Resolve(), "pev5egg");
            if (ctx.faction != null) sheet.Goodwill(ctx.faction, PeopleEvents5Math.EggGoodwill);
        }

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            CraterEgg.Give(hc);
            var f = hc?.faction;
            if (f != null && f.HasGoodwill && !f.IsPlayer) WorldSituationWorker.Goodwill(f, PeopleEvents5Math.EggGoodwill, GoodwillWhy.Gift);
        }
    }

    /// <summary>The tamer ({X} of {FACTION}) back a season after {BEAST} hatched, to see it grown; {A} its bonded one.</summary>
    public class HappeningWorker_CraterEggGrown : HappeningWorker_Visitors
    {
        private static EggRecord Due(Map map) =>
            PeopleEvents5Book.Instance?.eggs.FirstOrDefault(r => r != null && !r.done && !r.came && r.beast != null && !r.beast.Dead && r.beast.Spawned && r.beast.Map == map
                                                                 && r.hatched >= 0 && Find.TickManager.TicksGame >= r.back);

        protected override bool CanHappen(Map map, HappeningExtension ext) => PeoplesD.Friendly(PeoplesMath.Tamers) != null && Due(map) != null;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var r = Due(ctx.map);
            ctx.faction = r?.faction != null && PeoplesD.Theirs(r.faction, PeoplesMath.Tamers) ? r.faction : PeoplesD.Friendly(PeoplesMath.Tamers);
            if (r == null || ctx.faction == null) return false;
            ctx.pet = r.beast;
            ctx.beast = r.beast.LabelShort;
            ctx.a = r.beast.relations?.DirectRelations.FirstOrDefault(x => x.def == PawnRelationDefOf.Bond && x.otherPawn != null && !x.otherPawn.Dead)?.otherPawn
                    ?? r.MostTended() ?? PeoplesD.Best(ctx.map, SkillDefOf.Animals);
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Tamers_Tamer"), ctx.faction, ext.ages);
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return ctx.stranger != null && ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            PeopleEvents.AgeTags(tags, ctx.faction);
            if (ctx.pet != null && PokeWorldLink.IsPokemon(ctx.pet)) tags.Add("pokemon");
        }

        public override string Unavailable(HappeningContext ctx, int option) =>
            option == 0 && (ctx.pet == null || ctx.pet.Dead || !ctx.pet.Spawned || ctx.pet.Map != ctx.map) ? "CC_PEv5_BeastGone".Translate(ctx.beast ?? "?").Resolve() : null;

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option == 0 ? "show" : option == 1 ? "told" : "later");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The tamer's hour with the grown beast where it stands (PeopleWorkOn, Rimfolk_PEv5_EggLook), or only words of how it does (<see cref="look"/> false).</summary>
    public class DilemmaEffect_CraterEggGrown : DilemmaEffect
    {
        public bool look = true;

        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (ctx?.pet == null) return;
            if (look && PlayOuts.On) sheet.Play("CC_PEv5_Impact_EggLook".Translate(ctx.stranger?.LabelShort ?? "?", ctx.pet.LabelShort).Resolve(), "CC_PEv5_Impact_EggLookBrief".Translate().Resolve());
            if (ctx.faction != null) sheet.Goodwill(ctx.faction, look ? PeopleEvents5Math.GrownGoodwill : PeopleEvents5Math.GrownWordsGoodwill);
        }

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            if (hc?.pet == null) return;
            if (!look)
            {
                var f = hc.faction;
                if (f != null && f.HasGoodwill && !f.IsPlayer) WorldSituationWorker.Goodwill(f, PeopleEvents5Math.GrownWordsGoodwill, GoodwillWhy.Helped);
                Arrivals.SendAway(PeoplesG.Party(hc));
                return;
            }
            var job = CraterEgg.LookJob;
            if (hc.stranger == null || job == null || !PeopleWorkOn.Begin(hc, hc.stranger, hc.pet, job, LocalTargetInfo.Invalid, null, 0, PeopleEvents5Math.LookTicks, 6f))
                CraterEgg.LookedOver(hc.stranger, hc.pet);
        }
    }

    /// <summary>The tamer's hour with the grown beast done (CraterEgg.LookedOver).</summary>
    public class CraterEggLookDone : PeopleWorkDone
    {
        public override void Done(Visit v, Pawn worker, Pawn on) => CraterEgg.LookedOver(worker, on);

        public override void GaveUp(Visit v)
        {
            if (v?.map == null) return;
            Tell.Message(NewsKind.People, Importance.Notable, v.map, "CC_PEv_WorkGivenUp".Translate((v.x?.LabelShort ?? "?").Named("WORKER"), (v.a?.LabelShort ?? "?").Named("PAWN")).Resolve(),
                LookTargets.Invalid, MessageTypeDefOf.NeutralEvent);
        }
    }
}
