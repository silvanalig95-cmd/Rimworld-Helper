using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>Friendly visitors of some sort: made and walking in when the event fires.</summary>
    public abstract class HappeningWorker_Visitors : IncidentWorker_Happening
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) => Arrivals.Friendly() != null && Colonists(map).Count > 0;

        protected List<Pawn> MakeGroup(HappeningContext ctx, HappeningExtension ext, int n, FloatRange ages, string lastName = null)
        {
            var list = new List<Pawn>();
            for (int i = 0; i < n; i++) list.Add(Arrivals.Make(ctx.faction.def.basicMemberKind, ctx.faction, ages, null, lastName));
            ctx.stranger = list.FirstOrDefault();
            ctx.group = list.Skip(1).ToList();
            return list;
        }

        protected override void Abort(HappeningContext ctx)
        {
            foreach (var p in ctx.group.Prepend(ctx.stranger).Where(p => p != null && !p.Spawned)) Arrivals.Discard(p);
            ctx.stranger = null;
            ctx.group.Clear();
        }

        /// <summary>A spot where the colony gathers: a party spot, else the dining table, else just outside.</summary>
        protected static IntVec3? GatheringSpot(Map map)
        {
            var spot = map.listerBuildings.allBuildingsColonist.FirstOrDefault(b => b.def.defName == "PartySpot" || b.def.defName == "Campfire");
            if (spot != null) return spot.Position;
            var table = Avail.ColonyBuildings(map, Avail.IsEatingTable).FirstOrDefault();
            return table?.Position;
        }
    }

    // ===================== The storyteller =====================

    /// <summary>A travelling storyteller (a bard, a news-teller) stays the evening. Those who listen get a fine night out.</summary>
    public class HappeningWorker_Storyteller : HappeningWorker_Visitors
    {
        private static readonly ChronicleKind[] TaleKinds = { ChronicleKind.World, ChronicleKind.Stranger, ChronicleKind.AmbitionAchieved };

        /// <summary>A chronicle line that reads like a story (a beast felled, a contest won, a nemesis brought down), not a log entry.</summary>
        private static bool Tellable(ChronicleEntry e, int now) =>
            TaleKinds.Contains(e.kind) && now - e.tick < GenDate.TicksPerYear && !e.text.NullOrEmpty() && e.text.Length >= 30 && e.text.Length < 200
            && !e.text.Contains("\"") && !e.text.Contains(": ");

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = Arrivals.Friendly();
            if (ctx.faction == null) return false;
            MakeGroup(ctx, ext, 1, ext.ages);
            var r = ctx.stranger.skills?.GetSkill(SkillDefOf.Social);
            if (r != null && !r.TotallyDisabled && r.Level < 10) r.Level = Rand.RangeInclusive(10, 16);
            var comp = CouncilGameComponent.Instance;
            int now = Find.TickManager.TicksGame;
            // They've heard of the colony: something it did this past year, if it did anything worth telling.
            ctx.tale = comp?.chronicle.Where(e => Tellable(e, now)).RandomElementWithFallback()?.text;
            ctx.a = Colonists(ctx.map).Where(p => !p.DevelopmentalStage.Baby()).RandomElementByWeightWithFallback(p =>
                p.skills?.GetSkill(SkillDefOf.Social) is SkillRecord s && People.Passionate(s) ? 3f : 1f);
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            float hours = ext.stayHours.RandomInRange;
            if (Arrivals.Visit(new List<Pawn> { ctx.stranger }, ctx.map, ctx.faction, GatheringSpot(ctx.map), hours) == null) return false;
            var v = CouncilGameComponent.Instance.happenings.AddVisit("tales", def, ctx.map, ctx.stranger, ctx.a, null,
                Find.TickManager.TicksGame + Mathf.RoundToInt(hours * GenDate.TicksPerHour));
            return v != null;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags) => tags.Add(ctx.tale.NullOrEmpty() ? "notale" : "tale");

        /// <summary>Evenings: anyone awake near the storyteller listens for a while.</summary>
        public static bool Hour(Visit v, CouncilGameComponent comp, int now)
        {
            var x = v.x;
            if (x == null || x.Dead || !x.Spawned || x.Map != v.map || now > v.until) return false;
            int hour = GenLocalDate.HourOfDay(v.map);
            if (hour < 17 || hour > 23 || x.Downed) return true;
            var lines = v.Ext?.lines;
            if (!lines.NullOrEmpty()) Arrivals.Say(x, Flavor.PickText(lines));
            foreach (var p in v.map.mapPawns.FreeColonistsSpawned)
            {
                if (v.touched.Contains(p) || !p.Awake() || p.Drafted || p.InMentalState || p.Downed || p.Position.DistanceTo(x.Position) > 16f) continue;
                v.touched.Add(p);
                using var _cause = Causes.Push("CC_Cause_Tales".Translate(x.LabelShort).Resolve());
                CouncilGameComponent.GiveThought(p, v.Ext?.thought ?? CouncilDefOf.Council_Evt_HeardTales, x);
                p.needs?.joy?.GainJoy(0.15f, JoyKindDefOf.Social);
            }
            return true;
        }
    }

    // ===================== Mourners at a grave =====================

    /// <summary>Family of someone buried in the colony come to pay their respects, and leave something at the grave.</summary>
    public class HappeningWorker_GraveVisitors : HappeningWorker_Visitors
    {
        private static IEnumerable<Building_Grave> Graves(Map map) =>
            map.listerBuildings.allBuildingsColonist.OfType<Building_Grave>()
                .Where(g => g.HasCorpse && g.Corpse?.InnerPawn is Pawn d && d.RaceProps.Humanlike && d.Faction == Faction.OfPlayer && CameGrown(d));

        /// <summary>Someone who had a life (and people) before the colony: not a child born or raised here.</summary>
        private static bool CameGrown(Pawn d)
        {
            float here = d.records?.GetValue(RecordDefOf.TimeAsColonistOrColonyAnimal) ?? 0f;
            return d.ageTracker.AgeBiologicalTicks - (long)here >= GenDate.TicksPerYear * 16L;
        }

        protected override bool CanHappen(Map map, HappeningExtension ext) => base.CanHappen(map, ext) && Graves(map).Any();

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var grave = Graves(ctx.map).RandomElementWithFallback();
            ctx.faction = Arrivals.Friendly();
            if (grave == null || ctx.faction == null) return false;
            var dead = grave.Corpse.InnerPawn;
            ctx.place = grave;
            ctx.dead = dead.LabelShort;
            MakeGroup(ctx, ext, ext.count.RandomInRange, ext.ages, (dead.Name as NameTriple)?.Last);
            ctx.a = Colonists(ctx.map).Where(p => !p.DevelopmentalStage.Baby()).OrderByDescending(p => p.relations?.OpinionOf(dead) ?? 0).FirstOrDefault();
            ctx.count = Mathf.RoundToInt(ext.amount.RandomInRange / 5f) * 5;
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            float hours = ext.stayHours.RandomInRange;
            var all = ctx.group.Prepend(ctx.stranger).ToList();
            var map = ctx.map;
            IntVec3? spot = CellFinder.TryFindRandomCellNear(ctx.place.Position, map, 2, c => c.Standable(map) && c.GetFirstBuilding(map) == null, out var near) ? near : (IntVec3?)null;
            if (Arrivals.Visit(all, map, ctx.faction, spot, hours) == null) return false;
            var v = CouncilGameComponent.Instance.happenings.AddVisit("grave", def, ctx.map, ctx.stranger, ctx.a, ctx.group, Find.TickManager.TicksGame + Mathf.RoundToInt(hours * GenDate.TicksPerHour));
            v.place = ctx.place;
            v.stake = ctx.count;
            v.text = ctx.dead;
            return true;
        }

        /// <summary>When they reach the grave: a moment of silence, a gift left there, and the colony is touched.</summary>
        public static bool Hour(Visit v, CouncilGameComponent comp, int now)
        {
            if (now > v.until) return false;
            if (v.done) return true;
            var place = v.place;
            if (place == null || !place.Spawned || place.Map != v.map) return false;
            var mourner = v.group.Prepend(v.x).FirstOrDefault(p => p != null && p.Spawned && p.Map == v.map && !p.Downed && p.Position.DistanceTo(place.Position) <= 6f);
            if (mourner == null) return true;
            v.done = true;
            var lines = v.Ext?.lines;
            if (!lines.NullOrEmpty()) Arrivals.Say(mourner, Flavor.PickText(lines));
            if (v.stake > 0)
            {
                var silver = ThingMaker.MakeThing(ThingDefOf.Silver);
                silver.stackCount = v.stake;
                GenPlace.TryPlaceThing(silver, place.Position, v.map, ThingPlaceMode.Near);
            }
            var dead = (place as Building_Grave)?.Corpse?.InnerPawn;
            using var _cause = Causes.Push("CC_Cause_Mourners".Translate(v.text ?? "?").Resolve());
            foreach (var p in IncidentWorker_Happening.Colonists(v.map))
            {
                bool knew = dead != null && p.relations != null && p.relations.OpinionOf(dead) >= 10;
                CouncilGameComponent.GiveThought(p, CouncilDefOf.Council_Evt_DeadHonored, null, knew ? 1.5f : 1f);
            }
            string text = (v.stake > 0 ? "CC_Evt_MournersGift" : "CC_Evt_Mourners").Translate(v.text ?? "?", v.stake).Resolve();
            Messages.Message(text, new LookTargets(place), MessageTypeDefOf.PositiveEvent, false);
            comp.AddChronicle(new ChronicleEntry(ChronicleKind.World, text, v.a));
            return true;
        }
    }

    // ===================== Pilgrims =====================

    /// <summary>Hungry pilgrims ask for food. Feeding them costs a little; their gratitude travels.</summary>
    public class HappeningWorker_Pilgrims : HappeningWorker_Visitors
    {
        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = Arrivals.Friendly();
            if (ctx.faction == null) return false;
            int n = ext.count.RandomInRange;
            MakeGroup(ctx, ext, n, ext.ages);
            ctx.count = 20 * n;
            ctx.a = Colonists(ctx.map).Where(p => !p.DevelopmentalStage.Juvenile()).RandomElementByWeightWithFallback(p => p.story?.traits?.HasTrait(TraitDefOf.Kind) == true ? 3f : 1f);
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(ctx.group.Prepend(ctx.stranger).ToList(), ctx.map, ctx.faction, null, ext.stayHours.RandomInRange) != null;

        protected override string Extra(HappeningContext ctx) => "CC_Evt_PilgrimsFood".Translate(DilemmaEffect_Consume.RawFood(ctx.map), ctx.count).Resolve();

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            // Fed: their gratitude may come back down the road.
            if (option == 0 && !byDefault && ctx.faction != null) CouncilGameComponent.Instance?.happenings.Remember("pilgrims", null, ctx.faction);
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    // ===================== The scholar =====================

    /// <summary>A travelling scholar (an engineer, in an industrial colony) asks for a bed for the night, and talks shop.</summary>
    public class HappeningWorker_Scholar : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) => base.CanHappen(map, ext) && Find.ResearchManager.GetProject(null) != null;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = Arrivals.Friendly();
            var proj = Find.ResearchManager.GetProject(null);
            if (ctx.faction == null || proj == null) return false;
            ctx.tale = proj.label;
            MakeGroup(ctx, ext, 1, ext.ages);
            var r = ctx.stranger.skills?.GetSkill(SkillDefOf.Intellectual);
            if (r != null && !r.TotallyDisabled && r.Level < 10) r.Level = Rand.RangeInclusive(10, 17);
            ctx.a = Colonists(ctx.map).Where(p => !p.DevelopmentalStage.Juvenile())
                .OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Intellectual)?.Level ?? 0).FirstOrDefault();
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(new List<Pawn> { ctx.stranger }, ctx.map, ctx.faction, null, ext.stayHours.RandomInRange) != null;
    }

    // ===================== A lost child =====================

    /// <summary>A child from a neighbouring people wandered off and turned up here (Biotech).</summary>
    public class HappeningWorker_LostChild : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            ModsConfig.BiotechActive && Find.Storyteller.difficulty.ChildrenAllowed && base.CanHappen(map, ext);

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            // A people whose ordinary folk can be children (some modded kinds are adults only).
            ctx.faction = Arrivals.Friendly(f => f.def.basicMemberKind?.RaceProps?.Humanlike == true && f.def.basicMemberKind.minGenerationAge <= 6);
            if (ctx.faction == null) return false;
            Pawn x;
            try
            {
                x = Arrivals.Make(ctx.faction.def.basicMemberKind, ctx.faction, ext.ages, null, null, true);
            }
            catch
            {
                return false;
            }
            if (x == null || !x.DevelopmentalStage.Child())
            {
                Arrivals.Discard(x);
                return false;
            }
            ctx.stranger = x;
            ctx.a = Colonists(ctx.map).Where(p => !p.DevelopmentalStage.Juvenile()).RandomElementByWeightWithFallback(p => p.story?.traits?.HasTrait(TraitDefOf.Kind) == true ? 3f : 1f);
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(new List<Pawn> { ctx.stranger }, ctx.map, ctx.faction, null, ext.answerDays * 24f + 6f) != null;

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            // Taken home: one day, grown, they may come back to say thank you.
            if (option == 0 && ctx.stranger != null && !ctx.stranger.Dead && ctx.stranger.Faction == ctx.faction)
                CouncilGameComponent.Instance?.happenings.Remember("child", ctx.stranger, ctx.faction, null, null, false, ctx.a);
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    // ===================== A stray =====================

    /// <summary>A stray cat or dog (whatever kind of pet lives around here) follows a colonist home.</summary>
    public class HappeningWorker_StrayAnimal : IncidentWorker_Happening
    {
        private static IEnumerable<PawnKindDef> Kinds(Map map) =>
            DefDatabase<PawnKindDef>.AllDefsListForReading.Where(k => k.race?.race != null && k.RaceProps.Animal && !k.RaceProps.Dryad && !k.RaceProps.Insect
                                                                      && k.RaceProps.petness >= 0.5f && k.RaceProps.baseBodySize <= 1.1f && k.race.GetStatValueAbstract(StatDefOf.Wildness) <= 0.6f
                                                                      && map.mapTemperature.SeasonAndOutdoorTemperatureAcceptableFor(k.race, 5f));

        private static IEnumerable<Pawn> Walkers(Map map) =>
            Colonists(map).Where(p => !p.DevelopmentalStage.Baby() && p.Awake() && !p.Downed && !p.Drafted && !p.WorkTypeIsDisabled(WorkTypeDefOf.Handling));

        protected override bool CanHappen(Map map, HappeningExtension ext) => Kinds(map).Any() && Walkers(map).Any();

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.a = Walkers(ctx.map).RandomElementByWeightWithFallback(p => p.skills?.GetSkill(SkillDefOf.Animals) is SkillRecord r && People.Passionate(r) ? 3f
                : p.DevelopmentalStage.Child() ? 2f : 1f);
            var kind = Kinds(ctx.map).RandomElementByWeightWithFallback(k => k.RaceProps.petness);
            if (ctx.a == null || kind == null) return false;
            var animal = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, null, PawnGenerationContext.NonPlayer, forceGenerateNewPawn: true));
            ctx.pet = ctx.stranger = animal;
            ctx.thing = kind.race;
            return true;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            if (!Arrivals.Spawn(ctx.pet, ctx.map, ctx.a.Position)) return false;
            ctx.pet.SetFaction(Faction.OfPlayer);
            if (ctx.pet.playerSettings != null && TrainableUtility.CanBeMaster(ctx.a, ctx.pet, true)) ctx.pet.playerSettings.Master = ctx.a;
            return true;
        }

        protected override void Abort(HappeningContext ctx)
        {
            if (ctx.pet != null && !ctx.pet.Spawned && !ctx.pet.Discarded) Find.WorldPawns.PassToWorld(ctx.pet, PawnDiscardDecideMode.Discard);
            ctx.pet = ctx.stranger = null;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            if (ctx.a?.DevelopmentalStage.Child() == true) tags.Add("child");
            if (ctx.pet?.RaceProps.predator == true) tags.Add("hunter");
        }

        public override void Cleanup(HappeningContext ctx)
        {
            // Let go: it wanders off into the wild.
            var pet = ctx.pet;
            if (pet != null && pet.Spawned && pet.Faction == null) pet.mindState.exitMapAfterTick = Find.TickManager.TicksGame + GenDate.TicksPerDay / 2;
        }
    }
}
