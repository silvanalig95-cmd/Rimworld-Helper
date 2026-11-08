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
    // Release D's four peoples at the colony's gate (docs/design/peoples.md, "Release D"; docs/design/peoples-next.md; the
    // defs in Defs/IncidentDefs/Rimfolk_Events_PeoplesD.xml), each only from a people of its calling at peace with the
    // colony, and every one played out on the map. Here: the Lantern Wardens' hunt, pelt-buyer and lantern, and the Quiet
    // Bell's bell for the enemy dead, its funerals and its warning of the void; the Circuit of Tamers and the Wheel are in
    // PeoplesD2.cs, the Wheel's towns that move in WheelTowns.cs. Nothing here keeps a clock or saves anything of its own:
    // the hunt waits on the beast's end as one of the events' visits (HappeningTracker.visits), the rest are the play-outs'.

    /// <summary>What release D's touchpoints share: their people, their kinds of person.</summary>
    public static class PeoplesD
    {
        /// <summary>A people of this calling that could send someone now.</summary>
        public static bool Theirs(Faction f, string calling) => f != null && Peoples.CallingOf(f) == calling && Arrivals.Usable(f);

        /// <summary>One of the peoples of this calling at peace with the colony, or null.</summary>
        public static Faction Friendly(string calling) => Arrivals.Friendly(f => Theirs(f, calling));

        private static IEnumerable<PawnKindDef> Kinds(Faction f, PawnGroupKindDef group) =>
            f?.def?.pawnGroupMakers?.Where(m => m != null && (group == null || m.kindDef == group)).SelectMany(m => (m.options ?? new List<PawnGenOption>()).Concat(m.guards ?? new List<PawnGenOption>()))
                .Select(o => o.kind).Where(k => k?.RaceProps?.Humanlike == true) ?? Enumerable.Empty<PawnKindDef>();

        /// <summary>A kind of theirs that fights (their trackers, gravewardens, rangers, yard guards), else their ordinary member.</summary>
        public static PawnKindDef FighterKind(Faction f) =>
            Kinds(f, PawnGroupKindDefOf.Combat).Where(k => k.isFighter && !k.trader).RandomElementWithFallback() ?? Arrivals.PersonKind(f);

        /// <summary>Their trader (the pelt-buyer, the egg-trader, the fixer), else their ordinary member.</summary>
        public static PawnKindDef TraderKind(Faction f) =>
            f?.def?.pawnGroupMakers?.Where(m => m?.traders != null).SelectMany(m => m.traders).Select(o => o.kind).FirstOrDefault(k => k?.RaceProps?.Humanlike == true)
            ?? Arrivals.PersonKind(f);

        /// <summary>The kind of theirs whose label is this (a ranger), else their ordinary member.</summary>
        public static PawnKindDef KindNamed(Faction f, string defName) =>
            Kinds(f, null).FirstOrDefault(k => k.defName == defName) ?? Arrivals.PersonKind(f);

        /// <summary>The colony's grown people at home, up and about.</summary>
        public static List<Pawn> Able(Map map) => IncidentWorker_Happening.Colonists(map).Where(StorytellerEvents.Able).ToList();

        /// <summary>The colony's best at a skill who can do the thing (null: nobody).</summary>
        public static Pawn Best(Map map, SkillDef skill, Func<Pawn, bool> can = null) =>
            Able(map).Where(p => can == null || can(p)).OrderByDescending(p => p.skills?.GetSkill(skill)?.Level ?? 0).FirstOrDefault();

        /// <summary>
        /// Pawns of another people already on the map, taken out of the lord they came under and put under a visit at a
        /// spot for a few hours (the Wardens gathered at the carcass, the Wheel's runners at a wreck).
        /// </summary>
        public static Lord Gather(List<Pawn> pawns, Faction f, IntVec3 spot, float hours)
        {
            var here = pawns.Where(p => p != null && p.Spawned && !p.Dead && !p.Downed).ToList();
            if (here.Count == 0 || f == null) return null;
            foreach (var p in here) p.GetLord()?.RemovePawn(p);
            return LordMaker.MakeNewLord(f, new LordJob_VisitColony(f, spot, Mathf.RoundToInt(hours * GenDate.TicksPerHour)), here[0].Map, here);
        }

        /// <summary>A line from the event's own lines with these tags, said over their head.</summary>
        public static void Say(Pawn x, HappeningExtension ext, HappeningContext ctx, params string[] tags)
        {
            if (x == null || !x.Spawned || x.Dead || ext?.lines == null) return;
            string line = VarietyUtil.Line(ext.lines, ctx, tags);
            if (!line.NullOrEmpty()) Speech.Say(x, line);
        }

        /// <summary>Where visitors wait for the answer: the colony's gate.</summary>
        public static IntVec3? GateSpot(Map map)
        {
            var c = PlaySpots.GateCell(map, null);
            return c.IsValid ? c : (IntVec3?)null;
        }

        /// <summary>The job of working at a carcass or a wreck (skinning it, stripping it for parts).</summary>
        public static JobDef SkinJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PeoplesD_Skin");

        public static JobDef StripJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PeoplesD_Strip");

        /// <summary>A job at a carcass or a wreck is done: what it was for (the Wardens' hide, the Wheel's half of the parts).</summary>
        public static void WorkDone(Pawn pawn, Thing target, JobDef job)
        {
            if (pawn == null || target == null || target.Destroyed) return;
            try
            {
                if (job == SkinJob) WardensHunt.Skinned(pawn, target as Corpse);
                else if (job == StripJob) WheelWreck.Stripped(pawn, target as Corpse);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples D: work at a carcass or a wreck", e);
            }
        }

        /// <summary>
        /// A carcass or a wreck taken apart: its butcher products, a share (<paramref name="ours"/> of each, the rest)
        /// set down where it lay for the colony and the rest into the worker's pack, and the body gone. Returns what the
        /// colony got, as words.
        /// </summary>
        public static string Divide(Pawn worker, Corpse body, Func<Thing, int> ours)
        {
            var map = body.Map;
            var at = body.Position;
            var kept = new List<string>();
            foreach (var t in body.ButcherProducts(worker, 1f).ToList())
            {
                int n = Mathf.Clamp(ours(t), 0, t.stackCount);
                if (n < t.stackCount)
                {
                    var theirs = n > 0 ? t.SplitOff(t.stackCount - n) : t;
                    if (worker.inventory?.innerContainer == null || !worker.inventory.innerContainer.TryAdd(theirs)) theirs.Destroy();
                    if (n == 0) continue;
                }
                kept.Add(t.LabelShort);
                GenPlace.TryPlaceThing(t, at, map, ThingPlaceMode.Near);
            }
            if (!body.Destroyed) body.Destroy();
            return TextUtil.JoinAnd(kept);
        }
    }

    /// <summary>Working at a carcass or a wreck: up to it, a while at it (blood or sparks), then what it was for (PeoplesD.WorkDone).</summary>
    public class JobDriver_PeoplesWork : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            var work = ToilMaker.MakeToil("PeoplesWork");
            work.tickIntervalAction = delta =>
            {
                var t = job.targetA.Thing;
                if (t == null) return;
                pawn.rotationTracker.FaceTarget(t);
                if (pawn.IsHashIntervalTick(240, delta))
                {
                    if (job.def == PeoplesD.StripJob) FleckMaker.ThrowMicroSparks(t.DrawPos, pawn.Map);
                    else FleckMaker.ThrowDustPuff(t.Position, pawn.Map, 0.4f);
                }
            };
            work.defaultCompleteMode = ToilCompleteMode.Delay;
            work.defaultDuration = Mathf.Max(600, job.count);
            work.WithProgressBarToilDelay(TargetIndex.A);
            work.handlingFacing = true;
            yield return work;
            var done = ToilMaker.MakeToil("PeoplesWorkDone");
            done.initAction = () => PeoplesD.WorkDone(pawn, job.targetA.Thing, job.def);
            done.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return done;
        }
    }

    // ===================== The Lantern Wardens' hunt =====================

    /// <summary>
    /// A great beast is on the colony's land (an alpha that leads a pack, a beast nemesis, a legend of the region: Beasts/),
    /// wild and on its feet, and two or three Wardens of the crossroads come to the gate: they follow such beasts from
    /// lantern to lantern. They offer to hunt it beside the colony's best shot, or alone, for their share of the trophy:
    /// when it falls, one of them skins it where it lies, the hide going with the Wardens for their book and the meat
    /// left for the colony. Bidden, they go after it at once under the game's own lord for an assault on a thing; the
    /// colony's hunter, if sent, hunts it with the game's own hunting job. Their watch on the beast is one of the events'
    /// visits, ended when it falls, when it is gone, or after two days.
    /// </summary>
    public class HappeningWorker_WardensHunt : HappeningWorker_Visitors
    {
        public const string Kind = "wardenHunt";

        private static HediffDef legendDef;

        /// <summary>The great beast on the map: a legend, a nemesis, an alpha (the biggest story first), wild and up; "legend", "nemesis" or "alpha" its kind.</summary>
        public static Pawn Quarry(Map map, out string kind)
        {
            kind = null;
            if (map == null) return null;
            legendDef ??= DefDatabase<HediffDef>.GetNamedSilentFail("Council_Legendary");
            var nemeses = CouncilGameComponent.Instance?.nemeses;
            var alphas = BeastBook.Instance?.alphas;
            Pawn best = null;
            int rank = 0;
            foreach (var p in map.mapPawns.AllPawnsSpawned)
            {
                if (p == null || p.Dead || p.Downed || p.Faction != null || p.RaceProps?.Animal != true || p.Position.Fogged(map)) continue;
                bool legend = legendDef != null && p.health?.hediffSet?.HasHediff(legendDef) == true;
                bool nemesis = nemeses != null && nemeses.IsNemesis(p);
                bool alpha = alphas != null && alphas.Any(a => a != null && a.Open && a.state == "wild" && a.alpha == p);
                int r = PeoplesDMath.QuarryRank(legend, nemesis, alpha);
                if (r <= rank) continue;
                rank = r;
                best = p;
                kind = legend ? "legend" : nemesis ? "nemesis" : "alpha";
            }
            return best;
        }

        /// <summary>Someone of the colony who can hunt the beast: grown, up, not unable to fight.</summary>
        private static bool CanHunt(Pawn p) => p != null && !p.WorkTagIsDisabled(WorkTags.Violent) && p.skills?.GetSkill(SkillDefOf.Shooting)?.TotallyDisabled == false;

        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            PeoplesD.Friendly(PeoplesMath.Hunters) != null && Quarry(map, out _) != null && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Hunters);
            ctx.pet = Quarry(ctx.map, out string kind);
            if (ctx.faction == null || ctx.pet == null) return false;
            ctx.note = kind;
            ctx.beast = ctx.pet.LabelShort;
            int n = Rand.RangeInclusive(PeoplesDMath.HuntersMin, PeoplesDMath.HuntersMax);
            var list = new List<Pawn>();
            for (int i = 0; i < n; i++) list.Add(Arrivals.Make(PeoplesD.FighterKind(ctx.faction), ctx.faction, ext.ages));
            ctx.stranger = list.FirstOrDefault();
            ctx.group = list.Skip(1).ToList();
            ctx.a = PeoplesD.Best(ctx.map, SkillDefOf.Shooting, CanHunt);
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return ctx.stranger != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(ctx.group.Prepend(ctx.stranger).ToList(), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            if (!ctx.note.NullOrEmpty()) tags.Add(ctx.note);
            if (ctx.a == null) tags.Add("nohunter");
            if (ctx.pet != null && PackAlphas.IsManhunter(ctx.pet.MentalStateDef)) tags.Add("manhunter");
        }

        /// <summary>The Wardens go after the beast (the game's assault on a thing), the colony's hunter with them; the watch for its end begins.</summary>
        public static void Hunt(HappeningContext ctx, bool together)
        {
            var map = ctx?.map;
            var beast = ctx?.pet;
            var wardens = ctx?.group.Prepend(ctx.stranger).Where(p => p != null && p.Spawned && !p.Dead && !p.Downed && p.Map == map).ToList();
            if (map == null || beast == null || beast.Dead || !beast.Spawned || wardens == null || wardens.Count == 0 || ctx.faction == null) return;
            PeoplesD.Say(ctx.stranger, ctx.Ext, ctx, "hunt");
            var hunter = together ? ctx.a : null;
            try
            {
                foreach (var w in wardens) w.GetLord()?.RemovePawn(w);
                LordMaker.MakeNewLord(ctx.faction, new LordJob_AssaultThings(ctx.faction, new List<Thing> { beast }, 1f, false), map, wardens);
                if (hunter != null && hunter.Spawned && !hunter.Downed && !hunter.Drafted && !hunter.InMentalState)
                {
                    if (map.designationManager.DesignationOn(beast, DesignationDefOf.Hunt) == null) map.designationManager.AddDesignation(new Designation(beast, DesignationDefOf.Hunt));
                    if (WorkGiver_HunterHunt.HasHuntingWeapon(hunter)) hunter.jobs.TryTakeOrderedJob(JobMaker.MakeJob(JobDefOf.Hunt, beast), JobTag.Misc);
                }
                var v = CouncilGameComponent.Instance?.happenings?.AddVisit(Kind, ctx.incident, map, ctx.stranger, hunter, ctx.group, Find.TickManager.TicksGame + PeoplesDMath.HuntDays * GenDate.TicksPerDay);
                if (v != null) v.place = beast;
                Tell.Message(NewsKind.Land, Importance.Notable, map, "CC_PeoplesD_HuntSet".Translate(ctx.faction.Name, beast.LabelShort).Resolve(), new LookTargets(wardens.Cast<Thing>().Append(beast)),
                    MessageTypeDefOf.NeutralEvent);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples D: the Wardens' hunt", e);
            }
        }

        /// <summary>
        /// A beast died (HappeningTracker.Notify_Killed): if the Wardens were after it, they gather at the carcass and one
        /// of them skins it where it lies.
        /// </summary>
        public static void Killed(Pawn dead)
        {
            var visits = CouncilGameComponent.Instance?.happenings?.visits;
            if (dead == null || visits == null) return;
            foreach (var v in visits.Where(v => v.kind == Kind && !v.done && v.place == dead).ToList())
            {
                v.done = true;
                WardensHunt.Fell(v, dead);
            }
        }

        /// <summary>
        /// The watch on the beast, each hour: done when it is gone from the map or the Wardens are; after two days they give
        /// it up. A hunt given up, the Wardens still here walk home, and the news says so.
        /// </summary>
        public static bool Hour(Visit v, CouncilGameComponent comp, int now)
        {
            if (v.done) return false;
            var beast = v.place as Pawn;
            var wardens = v.group.Prepend(v.x).Where(p => p != null && p.Spawned && !p.Dead && !p.Downed && p.Map == v.map).ToList();
            bool here = beast != null && !beast.Destroyed && beast.Spawned && !beast.Dead && beast.Map == v.map;
            if (here && wardens.Count > 0 && now <= v.until) return true;
            if (wardens.Count > 0 && (beast == null || !beast.Dead)) GiveUp(v.map, wardens, beast);
            return false;
        }

        private static void GiveUp(Map map, List<Pawn> wardens, Pawn beast)
        {
            try
            {
                var f = wardens[0].Faction;
                foreach (var w in wardens) w.GetLord()?.RemovePawn(w);
                LordMaker.MakeNewLord(f, new LordJob_ExitMapBest(Pace.Errand, true, true), map, wardens);
                Tell.Message(NewsKind.Land, Importance.Flavor, map, "CC_PeoplesD_HuntGivenUp".Translate(f?.Name ?? "?", beast?.LabelShort ?? "?").Resolve(), new LookTargets(wardens),
                    MessageTypeDefOf.NeutralEvent);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples D: the Wardens give up the hunt", e);
            }
        }
    }

    /// <summary>
    /// The Wardens go after the beast at once (HappeningWorker_WardensHunt.Hunt): under the game's own lord for an assault
    /// on a thing; <see cref="together"/>, the colony's best shot with them on the game's own hunting job.
    /// </summary>
    public class DilemmaEffect_WardensHunt : DilemmaEffect
    {
        public bool together;

        public override bool ChanceEach => false;

        public override string Unavailable(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            if (ctx?.pet == null || ctx.pet.Dead || !ctx.pet.Spawned) return "CC_PeoplesD_BeastGone".Translate(hc?.beast ?? "?").Resolve();
            if (together && (ctx.a == null || ctx.a.Dead || ctx.a.Downed)) return "CC_PeoplesD_NoHunter".Translate().Resolve();
            return null;
        }

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (ctx?.pet == null) return;
            string beast = ctx.pet.LabelShort;
            sheet.Play((together ? "CC_PeoplesD_Impact_HuntTogether" : "CC_PeoplesD_Impact_HuntAlone").Translate(beast, ctx.a?.LabelShort ?? "?").Resolve(),
                "CC_PeoplesD_Impact_HuntBrief".Translate(beast).Resolve());
        }

        public override void Apply(DilemmaContext ctx) => HappeningWorker_WardensHunt.Hunt(ctx as HappeningContext, together);
    }

    /// <summary>What happens when the Wardens' beast falls: they gather at the carcass, and one skins it.</summary>
    public static class WardensHunt
    {
        public static void Fell(Visit v, Pawn dead)
        {
            var corpse = dead?.Corpse;
            var map = v.map;
            if (corpse == null || !corpse.Spawned || map == null || corpse.Map != map || PeoplesD.SkinJob == null) return;
            var wardens = v.group.Prepend(v.x).Where(p => p != null && p.Spawned && !p.Dead && !p.Downed && p.Map == map).ToList();
            if (wardens.Count == 0) return;
            var f = wardens[0].Faction;
            PeoplesD.Gather(wardens, f, corpse.Position, 4f);
            var lead = wardens.OrderBy(p => p.Position.DistanceToSquared(corpse.Position)).First();
            var job = JobMaker.MakeJob(PeoplesD.SkinJob, corpse);
            job.count = PeoplesDMath.SkinTicks;
            lead.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
            var ext = v.incident?.GetModExtension<HappeningExtension>();
            if (ext?.lines != null)
            {
                string line = Flavor.PickText(ext.lines.Where(l => l.StartsWith("[fell]")).Select(l => l.Substring(6).TrimStart()).ToList());
                if (!line.NullOrEmpty()) Speech.Say(lead, line);
            }
        }

        /// <summary>The hide off: into the Warden's pack for their book, the meat set down for the colony.</summary>
        public static void Skinned(Pawn warden, Corpse body)
        {
            if (body == null || !body.Spawned || warden == null) return;
            string beast = body.InnerPawn?.LabelShort ?? body.LabelShort;
            var map = body.Map;
            var at = body.Position;
            string kept = PeoplesD.Divide(warden, body, t => t.def.IsLeather ? 0 : t.stackCount);
            Tell.Message(NewsKind.Land, Importance.Notable, map,
                "CC_PeoplesD_Skinned".Translate(warden.Named("PAWN"), beast.Named("BEAST"), (kept.NullOrEmpty() ? "CC_PeoplesD_Nothing".Translate().Resolve() : kept).Named("KEPT")).Resolve(),
                new LookTargets(at, map), MessageTypeDefOf.PositiveEvent);
        }
    }

    // ===================== The Wardens' pelt-buyer =====================

    /// <summary>
    /// The colony has pelts in its stores (20 or more of one leather), and the Wardens' pelt-buyer comes to buy them at the
    /// book's rate, a little over their worth ({TALE} for {COUNT} silver); they are carried out to the buyer at the gate,
    /// and the silver carried in. The buyer's tracker may teach the colony's handler of beasts to track while they deal.
    /// </summary>
    public class HappeningWorker_PeltBuyer : HappeningWorker_Visitors
    {
        /// <summary>The leather the colony has most of in its stores, and how much.</summary>
        public static ThingDef Pelt(Map map, out int have)
        {
            have = 0;
            if (map == null) return null;
            ThingDef best = null;
            foreach (var g in map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver)
                         .Where(t => t.def.IsLeather && t.Spawned && !t.IsForbidden(Faction.OfPlayer) && t.IsInAnyStorage()).GroupBy(t => t.def))
            {
                int n = g.Sum(t => t.stackCount);
                if (n <= have) continue;
                have = n;
                best = g.Key;
            }
            return best;
        }

        protected override bool CanHappen(Map map, HappeningExtension ext)
        {
            if (PeoplesD.Friendly(PeoplesMath.Hunters) == null || Colonists(map).Count == 0) return false;
            Pelt(map, out int have);
            return PeoplesDMath.Pelts(have) > 0;
        }

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Hunters);
            ctx.thing = Pelt(ctx.map, out int have);
            if (ctx.faction == null || ctx.thing == null) return false;
            ctx.variant = PeoplesDMath.Pelts(have);
            if (ctx.variant <= 0) return false;
            ctx.count = PeoplesDMath.PeltPrice(ctx.variant, ctx.thing.BaseMarketValue);
            ctx.tale = VarietyUtil.Bundle(ctx.thing, ctx.variant);
            ctx.stranger = Arrivals.Make(PeoplesD.TraderKind(ctx.faction), ctx.faction, ext.ages);
            var tracker = Arrivals.Make(PeoplesD.FighterKind(ctx.faction), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            if (tracker != null) ctx.group.Add(tracker);
            ctx.b = tracker;
            ctx.a = PeoplesD.Best(ctx.map, SkillDefOf.Animals);
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(ctx.group.Prepend(ctx.stranger).ToList(), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        public override string Unavailable(HappeningContext ctx, int option) =>
            option <= 1 && ctx.thing != null && VarietyUtil.InStores(ctx.map, ctx.thing) < ctx.variant
                ? "CC_Dilemma_NotEnough".Translate(ctx.variant, ctx.thing.label).ToString()
                : null;

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            // Paid by the buyer at the gate: the silver carried in (Couriers/), the pelts carried out by the answer's errand.
            if (option <= 1 && !byDefault) Couriers.PaySilver(ctx.map, VarietyUtil.Near(ctx.a, ctx.map), ctx.count, Couriers.PeopleOf(ctx), ctx.stranger);
            if (!byDefault) PeoplesD.Say(ctx.stranger, Ext, ctx, option <= 1 ? "bought" : "refused");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    // ===================== A lantern at the crossroads =====================

    /// <summary>
    /// A beast took one of the colony within the last month (a beast nemesis that killed: Strangers/Nemeses.cs), and a Warden
    /// comes to the gate to light a lantern there for them, as the first Warden did for his brother: the Wardens will keep
    /// it lit until the beast is dead, and its page is open in their book. The colony may stand the lantern's first night
    /// with them, let them light it, or thank them. {DEAD} is the one taken, {BEAST} the beast.
    /// </summary>
    public class HappeningWorker_WardensLantern : HappeningWorker_Visitors
    {
        /// <summary>The most recent beast that killed one of the colony within the lantern's days, or null.</summary>
        public static Nemesis Taker()
        {
            var all = CouncilGameComponent.Instance?.nemeses?.all;
            if (all == null) return null;
            int now = Now;
            return all.Where(n => n != null && n.IsBeast && n.deed == "killed" && !n.victimName.NullOrEmpty() && PeoplesDMath.LanternFor(n.tick, now))
                .OrderByDescending(n => n.tick).FirstOrDefault();
        }

        // The Warden comes towards evening, lights the lantern at dusk and stands by it until first light.
        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            PeoplesD.Friendly(PeoplesMath.Hunters) != null && PeoplesMath.ShelterHour(GenLocalDate.HourOfDay(map)) && Taker() != null && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Hunters);
            var n = Taker();
            if (ctx.faction == null || n == null) return false;
            ctx.dead = n.victimName;
            ctx.beast = n.Called;
            ctx.note = n.ended ? "ended" : null;
            MakeGroup(ctx, ext, 1, ext.ages, null, false);
            if (ctx.stranger == null) return false;
            // The one who misses them most answers for the colony.
            var victim = n.victim;
            ctx.a = PeoplesD.Able(ctx.map).OrderByDescending(p => victim != null && p.relations != null ? p.relations.OpinionOf(victim) : 0).FirstOrDefault();
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(new List<Pawn> { ctx.stranger }, ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), PeoplesMath.HoursToLight(GenLocalDate.HourOfDay(ctx.map), 6)) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            if (ctx.note == "ended") tags.Add("ended");
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (option <= 1 && !byDefault) {
                if (ctx.note == "ended") PeoplesD.Say(ctx.stranger, Ext, ctx, "lit", "ended");
                else PeoplesD.Say(ctx.stranger, Ext, ctx, "lit");
            }
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    // ===================== The Quiet Bell: a bell for the enemy dead =====================

    /// <summary>
    /// The undertakers' event (Happenings/Workers_Callers.cs), as the Quiet Bell does it: after a fight, with the enemy's dead
    /// still lying out, bell-keepers of the Bell come to give them graves outside the wall or carry them off on their beast,
    /// and ring once over each, friend or foe. The raiders' own people hear of it (DilemmaEffect_BellForTheDead).
    /// </summary>
    public class HappeningWorker_BellForTheDead : HappeningWorker_Undertakers
    {
        protected override Faction People(HappeningExtension ext) => PeoplesD.Friendly(PeoplesMath.Sextons);

        protected override bool CanHappen(Map map, HappeningExtension ext) => PeoplesD.Friendly(PeoplesMath.Sextons) != null && base.CanHappen(map, ext);

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            base.AddTags(ctx, tags);
            if (DilemmaEffect_BellForTheDead.Peoples(ctx.map).Count > 0) tags.Add("theirpeople");
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (option <= 1 && !byDefault) PeoplesD.Say(ctx.stranger, Ext, ctx, "ring");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>
    /// The Bell rings for the enemy's dead: each people among them (three at most, never one that is everyone's enemy for
    /// good) hears of it and thinks a little better of the colony, the more of theirs seen to the more
    /// (PeoplesDMath.GoodwillFor).
    /// </summary>
    public class DilemmaEffect_BellForTheDead : DilemmaEffect
    {
        public override bool ChanceEach => false;

        /// <summary>The peoples of the enemy dead lying out, and how many of each.</summary>
        public static List<(Faction f, int n)> Peoples(Map map)
        {
            var list = new List<(Faction, int)>();
            if (map == null) return list;
            foreach (var g in HappeningWorker_Undertakers.Dead(map).Select(c => (c as Corpse)?.InnerPawn?.Faction)
                         .Where(f => f != null && !f.IsPlayer && !f.defeated && f.HasGoodwill && f.def.humanlikeFaction && !f.def.permanentEnemy).GroupBy(f => f)
                         .OrderByDescending(g => g.Count()).Take(PeoplesDMath.BellPeoples))
                list.Add((g.Key, g.Count()));
            return list;
        }

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            foreach (var (f, n) in Peoples(ctx?.map)) sheet.Goodwill(f, PeoplesDMath.GoodwillFor(n));
        }

        public override void Apply(DilemmaContext ctx)
        {
            var map = ctx?.map;
            foreach (var (f, n) in Peoples(map))
            {
                WorldSituationWorker.Goodwill(f, PeoplesDMath.GoodwillFor(n));
                Tell.Message(NewsKind.World, Importance.Flavor, map, "CC_PeoplesD_BellRung".Translate(f.Name, ctx.faction?.Name ?? "?").Resolve(), LookTargets.Invalid, MessageTypeDefOf.NeutralEvent);
            }
        }
    }

    // ===================== The Quiet Bell: a funeral =====================

    /// <summary>
    /// One of the colony died within the last few days and lies in a grave, and a bell-keeper of the Quiet Bell comes to
    /// that grave to lead the funeral, if the colony will have it: the colony gathered at the graveside while the
    /// bell-keeper rings once and says the Bell's words, or the bell rung by the keeper alone. {DEAD} is the dead,
    /// {PLACE} their grave-garden.
    /// </summary>
    public class HappeningWorker_BellFuneral : HappeningWorker_Visitors
    {
        /// <summary>The grave of one of the colony who died within the funeral's days (the most recent), or null.</summary>
        public static Building_Grave Fresh(Map map) =>
            map == null ? null : HappeningWorker_Setting.Graves(map).Where(g => g.Corpse != null && PeoplesDMath.Fresh(g.Corpse.Age)).OrderBy(g => g.Corpse.Age).FirstOrDefault();

        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            PeoplesD.Friendly(PeoplesMath.Sextons) != null && Fresh(map) != null && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Sextons);
            var grave = Fresh(ctx.map);
            if (ctx.faction == null || grave == null) return false;
            ctx.place = grave;
            var dead = grave.Corpse.InnerPawn;
            ctx.dead = dead?.LabelShort ?? "?";
            MakeGroup(ctx, ext, 1, ext.ages, null, false);
            if (ctx.stranger == null) return false;
            ctx.a = PeoplesD.Able(ctx.map).OrderByDescending(p => dead != null && p.relations != null ? p.relations.OpinionOf(dead) : 0).FirstOrDefault();
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(new List<Pawn> { ctx.stranger }, ctx.map, ctx.faction, ctx.place?.Position, ext.stayHours.RandomInRange) != null;

        public override string Unavailable(HappeningContext ctx, int option) =>
            option <= 1 && (ctx.place == null || ctx.place.Destroyed) ? "CC_PeoplesD_GraveGone".Translate(ctx.dead ?? "?").Resolve() : null;

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (option <= 1 && !byDefault) PeoplesD.Say(ctx.stranger, Ext, ctx, "ring");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    // ===================== The Quiet Bell: the void's signs =====================

    /// <summary>
    /// With Anomaly: the void is awake (the monolith past its first level) and none of the game's big threats has come for
    /// a week, and a bell-keeper comes to the gate to say what the Bell hears in the ground: something under it is
    /// awake, and it is better met with lights and a watch. The colony may keep a watch on its walls tonight with lamps at
    /// the doors, let the bell-keeper stand at the gate ringing through the night, or thank them. Nothing is promised:
    /// the words say what the Bell hears, not what will come.
    /// </summary>
    public class HappeningWorker_BellVoidSigns : HappeningWorker_Visitors
    {
        public static bool Stirring(Map map)
        {
            if (!ModsConfig.AnomalyActive || map == null) return false;
            try
            {
                var anomaly = Find.Anomaly;
                if (anomaly == null) return false;
                int days = (Find.TickManager.TicksGame - map.storyState.LastThreatBigTick) / GenDate.TicksPerDay;
                return PeoplesDMath.VoidStirring(anomaly.Level, days);
            }
            catch
            {
                return false;
            }
        }

        // The warning comes towards evening, and the bell-keeper stands at the gate until first light, as the words say.
        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            PeoplesD.Friendly(PeoplesMath.Sextons) != null && PeoplesMath.ShelterHour(GenLocalDate.HourOfDay(map)) && Stirring(map)
            && map.dangerWatcher.DangerRating == StoryDanger.None && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Sextons);
            if (ctx.faction == null) return false;
            MakeGroup(ctx, ext, 1, ext.ages, null, false);
            ctx.a = PeoplesD.Able(ctx.map).RandomElementWithFallback();
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return ctx.stranger != null && ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(new List<Pawn> { ctx.stranger }, ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), PeoplesMath.HoursToLight(GenLocalDate.HourOfDay(ctx.map), 6)) != null;

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesD.Say(ctx.stranger, Ext, ctx, option == 2 ? "thanks" : "ring");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }
}
