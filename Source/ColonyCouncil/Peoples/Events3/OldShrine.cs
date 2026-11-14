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
    // ===================== 15. An old shrine on the colony's ground (the pilgrim fellowship) =====================

    /// <summary>
    /// An old shrine on the colony's ground (item 15): pilgrims find something holy on the map (an ancient structure, a grave
    /// older than the colony, a great old tree, the land's own landmark), and two of them work three days setting stones and a
    /// lamp beside it (two with one of the colony working with them), and bless it; it becomes a building of the colony's
    /// (Rimfolk_OldShrine), with Ideology and Vanilla Ideology Expanded - Relics and Artifacts an inert relic laid at its foot.
    /// From then on pilgrims stop there in spring and summer and leave a little silver or herbs at its foot, and the colony's
    /// faithful feel it when they pray there (a joy of their own at it, and a good memory). Left out in the weather it wears;
    /// let fall to ruin, the fellowship mourns it.
    /// </summary>
    public static class OldShrine
    {
        public const string WorkKind = "pev:shrine";
        public const string StopKind = "pev:stop";
        public const string EventDef = "Rimfolk_Evt_OldShrine";

        public static ThingDef ShrineDef => DefDatabase<ThingDef>.GetNamedSilentFail("Rimfolk_OldShrine");

        public static JobDef StonesJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv3_SetStones");

        public static ThoughtDef PrayedThought => DefDatabase<ThoughtDef>.GetNamedSilentFail("Rimfolk_Thought_PrayedAtShrine");

        private static int Now => Find.TickManager.TicksGame;

        private static List<OldShrineRecord> Shrines => PeopleEvents3Book.Instance?.shrines;

        /// <summary>The shrine standing on this map, if any (one a map).</summary>
        public static OldShrineRecord On(Map map) => Shrines?.FirstOrDefault(r => r.map == map && !r.gone);

        /// <summary>
        /// Something holy on the map, the strongest first (PeopleEvents3Math.HolyKinds): an ancient structure out in the open,
        /// a grave the colony didn't dig with someone in it, a great old tree, the land's landmark. <paramref name="kind"/> and
        /// <paramref name="name"/> say what it is; <paramref name="at"/> is where the shrine would stand beside it.
        /// </summary>
        public static Thing Holy(Map map, out string kind, out string name, out IntVec3 at)
        {
            kind = name = null;
            at = IntVec3.Invalid;
            if (map == null || ShrineDef == null) return null;
            var centre = CampSite.ColonyCenter(map);
            if (!centre.IsValid) centre = map.Center;
            var ancient = map.listerThings.AllThings.OfType<Building>()
                .Where(b => b.def.defName.StartsWith("Ancient", StringComparison.Ordinal) && b.Faction == null && !b.Fogged() && !Plain(b.def) && b.Spawned)
                .OrderByDescending(b => b.def.size.Area).ThenBy(b => b.Position.DistanceToSquared(centre)).ToList();
            foreach (var b in ancient)
                if (Site(map, b, out at))
                {
                    kind = "ancient";
                    name = b.def.label;
                    return b;
                }
            var graves = map.listerThings.ThingsInGroup(ThingRequestGroup.Grave).OfType<Building_Grave>().Where(g => g.Faction != Faction.OfPlayer && g.HasCorpse && !g.Fogged()).ToList();
            foreach (var g in graves)
                if (Site(map, g, out at))
                {
                    kind = "grave";
                    name = g.def.label;
                    return g;
                }
            float years = PeopleEvents3Math.OldTreeYears * GenDate.TicksPerYear;
            var trees = map.listerThings.ThingsInGroup(ThingRequestGroup.Plant).OfType<Plant>()
                .Where(p => p.def.plant != null && p.def.plant.IsTree && p.Growth >= 0.99f && p.Age >= years && !p.Fogged() && p.Faction == null && !p.def.defName.Contains("Gauranlen"))
                .OrderByDescending(p => p.Age).Take(20).ToList();
            foreach (var t in trees)
                if (Site(map, t, out at))
                {
                    kind = "tree";
                    name = t.def.label;
                    return t;
                }
            string land = OldPlaces.Landmark(map);
            if (land != null)
            {
                var far = map.AllCells.Where(c => (c.x + c.z) % 7 == 0 && c.Standable(map) && !c.Fogged(map) && !c.Roofed(map) && !(map.areaManager.Home?[c] ?? false) && c.DistanceToEdge(map) > 12)
                    .OrderByDescending(c => c.DistanceToSquared(centre)).FirstOrDefault(c => Free(map, c));
                if (far.IsValid && far != IntVec3.Zero)
                {
                    kind = "landmark";
                    name = land;
                    at = far;
                    return null;
                }
            }
            return null;
        }

        /// <summary>Walls, fences, floors and their like are no shrine's place.</summary>
        private static bool Plain(ThingDef d)
        {
            string n = d.defName;
            return d.IsFrame || d.building?.isNaturalRock == true || n.Contains("Wall") || n.Contains("Fence") || n.Contains("Barrier") || n.Contains("Door") || n.Contains("Floor") || n.Contains("Tile")
                   || n.Contains("Pipe") || n.Contains("Cable") || n.Contains("Conduit");
        }

        private static bool Free(Map map, IntVec3 c)
        {
            var def = ShrineDef;
            if (def == null || !c.InBounds(map) || !c.Standable(map) || c.GetEdifice(map) != null || c.GetFirstItem(map) != null || c.GetFirstPawn(map) != null || c.Fogged(map)) return false;
            if (c.GetTerrain(map)?.IsWater ?? true) return false;
            var inter = c + def.interactionCellOffset;
            return inter.InBounds(map) && inter.Standable(map) && inter.GetEdifice(map) == null && (def.terrainAffordanceNeeded == null || c.SupportsStructureType(map, def.terrainAffordanceNeeded));
        }

        /// <summary>A free cell beside the holy thing for the shrine, not where a shrine stood before.</summary>
        private static bool Site(Map map, Thing t, out IntVec3 at)
        {
            at = IntVec3.Invalid;
            var used = Shrines?.Where(r => r.map == map).Select(r => r.cell).ToList() ?? new List<IntVec3>();
            foreach (var c in GenRadial.RadialCellsAround(t.Position, 4f, false))
            {
                if (t.OccupiedRect().Contains(c) || used.Any(u => u.InHorDistOf(c, 8f)) || !Free(map, c)) continue;
                if (t.OccupiedRect().ClosestCellTo(c).DistanceTo(c) < 1.5f) continue;
                at = c;
                return true;
            }
            return false;
        }

        // ---------------- The work ----------------

        /// <summary>The work set going: the two pilgrims (and the colony's helper) at the spot by day, their days counted at dusk.</summary>
        public static Visit Begin(HappeningContext ctx, bool helped)
        {
            var t = CouncilGameComponent.Instance?.happenings;
            var party = PeoplesG.Party(ctx).Where(p => p.Spawned && !p.Dead).ToList();
            if (t == null || party.Count == 0 || !ctx.spot.IsValid) return null;
            int days = PeopleEvents3Math.ShrineDays(helped);
            var v = t.AddVisit(WorkKind, ctx.incident, ctx.map, party[0], helped ? ctx.b : null, party.Skip(1).ToList(), Now + (days + 3) * GenDate.TicksPerDay);
            v.cell = ctx.spot;
            v.place = ctx.place;
            v.stake = 0;
            v.due = days;
            v.text = ctx.faction.GetUniqueLoadID() + "|" + (ctx.note ?? "ancient") + "|" + (ctx.tale ?? "?").Replace("|", "/");
            try
            {
                foreach (var p in party) Hosting.Provision(p, days + 1f);
                PeoplesD.Gather(party, ctx.faction, ctx.spot, (days + 1) * 24f);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 3: the pilgrims' stay at the shrine", e);
            }
            return v;
        }

        private static string[] Parts(Visit v) => (v?.text ?? "").Split('|');

        /// <summary>The work's hour: by day they set stones and the lamp (their job), at dusk a day counted, on the last the blessing.</summary>
        public static bool WorkHour(Visit v, int now)
        {
            if (v.done) return false;
            var map = v.map;
            var f = PeoplesF.PeopleOf(v);
            var workers = v.group.Prepend(v.x).Where(p => p != null && p.Spawned && !p.Dead && !p.Downed && p.Map == map).ToList();
            if (map == null || f == null || workers.Count == 0 || f.HostileTo(Faction.OfPlayer) || now > v.until)
            {
                v.done = true;
                Arrivals.SendAway(workers);
                return false;
            }
            int hour = GenLocalDate.HourOfDay(map);
            if (hour >= PeopleEvents3Math.ShrineFrom && hour < PeopleEvents3Math.ShrineTo)
            {
                var job = StonesJob;
                foreach (var p in workers.Concat(Helper(v)))
                {
                    if (job == null || p.CurJobDef == job || p.InMentalState || p.Drafted) continue;
                    if (!CellFinder.TryFindRandomCellNear(v.cell, map, 2, c => c != v.cell && c.Standable(map) && p.CanReach(c, PathEndMode.OnCell, Danger.Some), out var stand)) continue;
                    var j = JobMaker.MakeJob(job, stand, v.cell);
                    j.count = 2 * GenDate.TicksPerHour;
                    p.jobs.StartJob(j, JobCondition.InterruptForced, null, false, true);
                }
            }
            if (hour == PeopleEvents3Math.ShrineTo && v.skill == null)
            {
                // A day's work done (v.skill marks the day counted until the next morning).
                v.stake++;
                v.skill = SkillDefOf.Construction;
                if (v.stake >= v.due) Bless(v, f, workers);
                else PeoplesG.SayLine(workers[0], v.incident, "day");
            }
            if (hour == PeopleEvents3Math.ShrineFrom) v.skill = null;
            return !v.done;
        }

        /// <summary>The colony's helper, while they are up and free to work beside the pilgrims.</summary>
        private static IEnumerable<Pawn> Helper(Visit v)
        {
            var a = v.a;
            if (a != null && a.Spawned && !a.Dead && !a.Downed && a.Map == v.map && !a.Drafted && !a.InMentalState && (a.needs?.food == null || !a.needs.food.Starving)) yield return a;
        }

        /// <summary>The last day's dusk: the stones and the lamp blessed, the shrine the colony's, a relic laid at its foot with the mod.</summary>
        private static void Bless(Visit v, Faction f, List<Pawn> workers)
        {
            v.done = true;
            var map = v.map;
            var def = ShrineDef;
            var parts = Parts(v);
            string kind = parts.Length > 1 ? parts[1] : "ancient";
            string holyName = parts.Length > 2 ? parts[2] : "?";
            Thing shrine = null;
            try
            {
                // Whoever stands there steps aside, and what lies there is cleared for the stones.
                if (def != null && v.cell.InBounds(map) && v.cell.Standable(map) && v.cell.GetEdifice(map) == null && !(v.cell.GetTerrain(map)?.IsWater ?? true))
                {
                    var made = ThingMaker.MakeThing(def, def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null);
                    made.SetFaction(Faction.OfPlayer);
                    shrine = GenSpawn.Spawn(made, v.cell, map, Rot4.North);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 3: the shrine raised", e);
            }
            if (shrine == null)
            {
                Tell.Message(NewsKind.World, Importance.Notable, map, "CC_PEv3_ShrineBlocked".Translate((f?.Name ?? "?").Named("FACTION")).Resolve(), new LookTargets(new TargetInfo(v.cell, map)),
                    MessageTypeDefOf.NeutralEvent);
                Arrivals.SendAway(workers);
                return;
            }
            PeopleEvents3Book.Instance?.AddShrine(new OldShrineRecord { map = map, shrine = shrine, cell = shrine.Position, faction = f, holy = kind, holyName = holyName, built = Now });
            FleckMaker.ThrowLightningGlow(shrine.DrawPos, map, 1.5f);
            PeoplesG.SayLine(workers[0], v.incident, "bless");
            string relic = null;
            if (ModsConfig.IdeologyActive)
            {
                var token = PeopleEvents2Math.RelicTokens.Select(n => DefDatabase<ThingDef>.GetNamedSilentFail(n)).Where(d => d != null).RandomElementWithFallback();
                if (token != null && GenPlace.TryPlaceThing(ThingMaker.MakeThing(token, GenStuff.DefaultStuffFor(token)), v.cell + IntVec3.South, map, ThingPlaceMode.Near, out var laid))
                    relic = laid.LabelNoCount;
            }
            if (v.a != null && !v.a.Dead) v.a.skills?.Learn(SkillDefOf.Construction, 1200f, true);
            if (f != null && f.HasGoodwill && !f.IsPlayer) WorldSituationWorker.Goodwill(f, PeopleEvents3Math.ShrineGoodwill, false);
            FactionMemory.Note(f, "welcomed", "CC_PEv3_ShrineDeed".Translate().Resolve());
            Tell.Letter(NewsKind.World, Importance.Notable, map, "CC_PEv3_ShrineLabel".Translate().Resolve(),
                ("CC_PEv3_ShrineBlessed_" + kind).Translate((f?.Name ?? "?").Named("FACTION"), holyName.Named("HOLY")).Resolve()
                + (relic != null ? " " + "CC_PEv3_ShrineRelic".Translate(relic.Named("RELIC")).Resolve() : ""),
                LetterDefOf.PositiveEvent, new LookTargets(shrine), f);
            Arrivals.SendAway(workers);
        }

        // ---------------- Its seasons ----------------

        /// <summary>
        /// The homes' hour: each shrine weathers a little at midnight when out in the open (the colony mends it as any building
        /// of its own), fallen to ruin it is mourned (kept again, the fellowship says so); in spring and summer a pilgrim may stop
        /// there in the morning.
        /// </summary>
        public static void Watch(Map map, int now)
        {
            var list = Shrines;
            if (list == null) return;
            int hour = GenLocalDate.HourOfDay(map);
            foreach (var r in list.Where(r => r.map == map && !r.gone).ToList())
            {
                var s = r.shrine;
                if (s == null || s.Destroyed || !s.Spawned || s.Map != map)
                {
                    r.gone = true;
                    if (!r.ruined) Mourn(r, map);
                    continue;
                }
                if (hour == 0 && !s.Position.Roofed(map) && s.def.useHitPoints)
                    s.HitPoints = Mathf.Max(1, s.HitPoints - Mathf.Max(1, Mathf.CeilToInt(s.MaxHitPoints * PeopleEvents3Math.WeatherPerDay)));
                if (!r.ruined && PeopleEvents3Math.Ruined(s.HitPoints, s.MaxHitPoints)) Mourn(r, map);
                else if (r.ruined && PeopleEvents3Math.Kept(s.HitPoints, s.MaxHitPoints))
                {
                    r.ruined = false;
                    News.Post("CC_PEv3_ShrineKept".Translate((r.faction?.Name ?? "?").Named("FACTION")).Resolve(), new LookTargets(s), NewsKind.World);
                }
                if (hour == 9 && !r.ruined) Stop(r, map, now);
            }
        }

        private static void Mourn(OldShrineRecord r, Map map)
        {
            r.ruined = true;
            r.mournedAt = Now;
            var f = r.faction;
            if (f != null && !f.defeated && f.HasGoodwill && !f.IsPlayer) WorldSituationWorker.Goodwill(f, PeopleEvents3Math.MournGoodwill, false);
            Tell.Message(NewsKind.World, Importance.Notable, map, "CC_PEv3_ShrineMourned".Translate((f?.Name ?? "?").Named("FACTION"), (r.holyName ?? "?").Named("HOLY")).Resolve(),
                r.shrine != null && r.shrine.Spawned ? new LookTargets(r.shrine) : LookTargets.Invalid, MessageTypeDefOf.NegativeEvent);
        }

        /// <summary>A pilgrim stops at the shrine on the road: spring and summer, now and then, not soon again.</summary>
        private static void Stop(OldShrineRecord r, Map map, int now)
        {
            var season = GenLocalDate.Season(map);
            if (season != Season.Spring && season != Season.Summer && season != Season.PermanentSummer) return;
            if (r.lastStop >= 0 && now - r.lastStop < PeopleEvents3Math.StopGapDays * GenDate.TicksPerDay) return;
            if (MapFacts.ThreatOn(map)) return;
            var f = PeoplesD.Friendly(PeoplesMath.Pilgrims);
            if (f == null || !Rand.Chance(PeopleEvents3Math.StopChance * StoryDirection.Factor(StoryWaysMath.Visitors))) return;
            var t = CouncilGameComponent.Instance?.happenings;
            var p = Arrivals.Make(PeoplesD.KindNamed(f, "Rimfolk_Pilgrims_Pilgrim"), f, new FloatRange(20f, 70f));
            if (t == null || p == null) return;
            var interact = r.shrine.InteractionCell;
            if (Arrivals.Visit(new List<Pawn> { p }, map, f, interact, 6f) == null)
            {
                Arrivals.Discard(p);
                return;
            }
            r.lastStop = now;
            var v = t.AddVisit(StopKind, DefDatabase<IncidentDef>.GetNamedSilentFail(EventDef), map, p, null, null, now + 8 * GenDate.TicksPerHour);
            v.place = r.shrine;
            v.text = f.GetUniqueLoadID();
        }

        /// <summary>The pilgrim's stop: at the shrine, a while in prayer, the offering at its foot, and on.</summary>
        public static bool StopHour(Visit v, int now)
        {
            if (v.done) return false;
            var p = v.x;
            var s = v.place;
            if (p == null || p.Dead || !p.Spawned || s == null || !s.Spawned || now > v.until)
            {
                v.done = true;
                if (p != null && p.Spawned) Arrivals.SendAway(new[] { p });
                return false;
            }
            if (!p.Position.InHorDistOf(s.Position, 3f))
            {
                if (p.CurJobDef != JobDefOf.Goto && p.CanReach(s.InteractionCell, PathEndMode.OnCell, Danger.Some))
                    p.jobs.StartJob(JobMaker.MakeJob(JobDefOf.Goto, s.InteractionCell), JobCondition.InterruptForced, null, false, true);
                return true;
            }
            if (v.stake == 0)
            {
                v.stake = 1;
                var pray = JobMaker.MakeJob(JobDefOf.Wait, p.Position, s);
                pray.expiryInterval = GenDate.TicksPerHour;
                p.jobs.StartJob(pray, JobCondition.InterruptForced, null, false, true);
                PeoplesG.SayLine(p, v.incident, "stop");
                return true;
            }
            v.done = true;
            // What they leave at its foot: a little silver, or herbs.
            var f = PeoplesF.PeopleOf(v);
            bool herbs = Rand.Bool;
            var def = herbs ? ThingDefOf.MedicineHerbal : ThingDefOf.Silver;
            int n = herbs ? PeopleEvents3Math.OfferingHerbs(Rand.Value) : PeopleEvents3Math.OfferingSilver(Rand.Value);
            foreach (var t in Couriers.Make(def, n))
                if (GenPlace.TryPlaceThing(t, s.InteractionCell, s.Map, ThingPlaceMode.Near, out var laid))
                    laid?.SetForbidden(false, false);
            News.Post("CC_PEv3_ShrineStop".Translate(p.Named("PILGRIM"), (f?.Name ?? "?").Named("FACTION"), n.Named("COUNT"), def.label.Named("WHAT")).Resolve(), new LookTargets(s), NewsKind.World);
            Arrivals.SendAway(new[] { p });
            return false;
        }

        /// <summary>One of the colony prayed at the shrine (JobDriver_ShrinePray): the faithful feel it.</summary>
        public static void Prayed(Pawn p)
        {
            var t = PrayedThought;
            if (t == null || p?.needs?.mood?.thoughts?.memories == null) return;
            // With Ideology, those who hold a faith; without it, everyone who prays there.
            if (ModsConfig.IdeologyActive && p.Ideo == null) return;
            p.needs.mood.thoughts.memories.TryGainMemory(t);
        }

        /// <summary>Dev mode: a pilgrim stops at this map's shrine within the hour, whatever the season and the gap.</summary>
        public static bool StopNow(Map map)
        {
            var r = On(map);
            if (r?.shrine == null || !r.shrine.Spawned) return false;
            var f = PeoplesD.Friendly(PeoplesMath.Pilgrims);
            var t = CouncilGameComponent.Instance?.happenings;
            var p = f == null ? null : Arrivals.Make(PeoplesD.KindNamed(f, "Rimfolk_Pilgrims_Pilgrim"), f, new FloatRange(20f, 70f));
            if (p == null || t == null || Arrivals.Visit(new List<Pawn> { p }, map, f, r.shrine.InteractionCell, 6f) == null) return false;
            r.lastStop = Now;
            var v = t.AddVisit(StopKind, DefDatabase<IncidentDef>.GetNamedSilentFail(EventDef), map, p, null, null, Now + 8 * GenDate.TicksPerHour);
            v.place = r.shrine;
            v.text = f.GetUniqueLoadID();
            return true;
        }
    }

    /// <summary>
    /// Two pilgrims of {FACTION} at the gate: they have found something holy on the colony's ground ({TALE}, a thing of the
    /// kind in the tags) and would set stones and a lamp beside it and bless it, three days' work. {B} is the colony's helper.
    /// </summary>
    public class HappeningWorker_OldShrine : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext)
        {
            if (PeoplesD.Friendly(PeoplesMath.Pilgrims) == null || OldShrine.On(map) != null || Colonists(map).Count == 0) return false;
            OldShrine.Holy(map, out _, out _, out var at);
            return at.IsValid;
        }

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Pilgrims);
            if (ctx.faction == null) return false;
            ctx.place = OldShrine.Holy(ctx.map, out string kind, out string name, out var at);
            if (!at.IsValid) return false;
            ctx.spot = at;
            ctx.note = kind;
            ctx.tale = name;
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Pilgrims_Keeper"), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            var second = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Pilgrims_Pilgrim"), ctx.faction, ext.ages);
            if (second != null) ctx.group.Add(second);
            ctx.a = KinMemory.Greeter(ctx.faction, ctx.map) ?? PeoplesD.Able(ctx.map).FirstOrDefault();
            // The helper: the colony's most faithful who can build, else its best builder.
            ctx.b = PeoplesD.Able(ctx.map).Where(p => p != ctx.a && !p.WorkTypeIsDisabled(WorkTypeDefOf.Construction))
                .OrderByDescending(p => Faiths.Holds(p) ? Faiths.Certainty(p) : 0f).ThenByDescending(p => p.skills?.GetSkill(SkillDefOf.Construction)?.Level ?? 0).FirstOrDefault();
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            PeopleEvents.AgeTags(tags, ctx.faction);
            if (!ctx.note.NullOrEmpty()) tags.Add(ctx.note);
            if (ctx.b != null) tags.Add("second");
        }

        protected override LookTargets Look(HappeningContext ctx) =>
            ctx.place != null && ctx.place.Spawned ? new LookTargets(ctx.place) : ctx.spot.IsValid ? new LookTargets(new TargetInfo(ctx.spot, ctx.map)) : base.Look(ctx);

        public override string Unavailable(HappeningContext ctx, int option)
        {
            if (option == 1 && (ctx.b == null || ctx.b.Dead || !ctx.b.Spawned)) return "CC_PEv3_NoHelper".Translate().Resolve();
            return null;
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option <= 1 ? "raise" : "thanks");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The pilgrims raise the shrine (OldShrine.Begin): three days, or two with <see cref="helped"/> (B working beside them).</summary>
    public class DilemmaEffect_OldShrine : DilemmaEffect
    {
        public bool helped;

        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc == null) return;
            int days = PeopleEvents3Math.ShrineDays(helped);
            sheet.Play("CC_PEv3_Impact_Shrine".Translate(hc.tale ?? "?", days).Resolve(), "CC_PEv3_Impact_ShrineBrief".Translate().Resolve());
            sheet.Add(ImpactKind.Other, 1, "CC_PEv3_Impact_ShrineStops".Translate().Resolve(), "CC_PEv3_Impact_ShrineStopsBrief".Translate().Resolve(), "pev3shrinestops");
            sheet.Add(ImpactKind.Goodwill, -1, "CC_PEv3_Impact_ShrineRuin".Translate(hc.faction?.Name ?? "?").Resolve(), "CC_PEv3_Impact_ShrineRuinBrief".Translate().Resolve(), "pev3shrineruin");
            if (hc.faction != null) sheet.Goodwill(hc.faction, PeopleEvents3Math.ShrineGoodwill);
            if (helped && hc.b != null) DilemmaImpact.Xp(sheet, hc.b.LabelShort, hc.b.ThingID, SkillDefOf.Construction, 1200f, false);
        }

        public override void Apply(DilemmaContext ctx) => OldShrine.Begin(ctx as HappeningContext, helped);
    }

    /// <summary>One of the colony prays at the shrine (target A the shrine, B where they kneel): the game's joy of it, and the faithful feel it (OldShrine.Prayed).</summary>
    public class JobDriver_ShrinePray : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) =>
            pawn.Reserve(job.targetA, job, job.def.joyMaxParticipants, 0, null, errorOnFailed) && pawn.ReserveSittableOrSpot(job.targetB.Cell, job, errorOnFailed);

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);
            var pray = ToilMaker.MakeToil("ShrinePray");
            pray.tickIntervalAction = delta =>
            {
                pawn.rotationTracker.FaceTarget(job.targetA);
                JoyUtility.JoyTickCheckEnd(pawn, delta, JoyTickFullJoyAction.EndJob, 1f, job.targetA.Thing as Building);
            };
            pray.handlingFacing = true;
            pray.defaultCompleteMode = ToilCompleteMode.Delay;
            pray.defaultDuration = job.def.joyDuration;
            pray.AddFinishAction(() =>
            {
                if (pawn.Spawned && pawn.Position.InHorDistOf(job.targetA.Cell, 2f)) OldShrine.Prayed(pawn);
            });
            yield return pray;
        }
    }

    /// <summary>A pilgrim (or the colony's helper) sets stones and the lamp by the shrine's spot (target A where they stand, B the spot): a while at it, dust puffs.</summary>
    public class JobDriver_SetStones : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);
            var work = ToilMaker.MakeToil("SetStones");
            work.tickIntervalAction = delta =>
            {
                pawn.rotationTracker.FaceCell(job.targetB.Cell);
                if (pawn.IsHashIntervalTick(400, delta)) FleckMaker.ThrowDustPuff(job.targetB.Cell, pawn.Map, 0.7f);
                if (pawn.IsColonist && pawn.IsHashIntervalTick(60, delta)) pawn.skills?.Learn(SkillDefOf.Construction, 0.06f * 60);
            };
            work.WithProgressBarToilDelay(TargetIndex.A);
            work.handlingFacing = true;
            work.defaultCompleteMode = ToilCompleteMode.Delay;
            work.defaultDuration = Mathf.Max(600, job.count);
            yield return work;
        }
    }
}
