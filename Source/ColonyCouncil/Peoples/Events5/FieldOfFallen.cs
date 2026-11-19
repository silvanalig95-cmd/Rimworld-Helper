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
    // ===================== 38. The field of the fallen (the Quiet Bell) =====================

    /// <summary>The field of the fallen's days: the dead it came for, their row of graves, the dusk's bell, the mourners and how it turned.</summary>
    public class FallenState : CampWorkState
    {
        public List<Thing> dead = new List<Thing>();
        public List<IntVec3> plots = new List<IntVec3>();
        public int rot, buried, rungDay = -1;
        public bool planned, gifts, mourners, stirRolled, stirred, claimRolled, claimed;
        public Faction claimPeople;

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref dead, "dead", LookMode.Reference);
            Scribe_Collections.Look(ref plots, "plots", LookMode.Value);
            Scribe_Values.Look(ref rot, "rot");
            Scribe_Values.Look(ref buried, "buried");
            Scribe_Values.Look(ref rungDay, "rungDay", -1);
            Scribe_Values.Look(ref planned, "planned");
            Scribe_Values.Look(ref gifts, "gifts");
            Scribe_Values.Look(ref mourners, "mourners");
            Scribe_Values.Look(ref stirRolled, "stirRolled");
            Scribe_Values.Look(ref stirred, "stirred");
            Scribe_Values.Look(ref claimRolled, "claimRolled");
            Scribe_Values.Look(ref claimed, "claimed");
            Scribe_References.Look(ref claimPeople, "claimPeople");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                dead ??= new List<Thing>();
                dead.RemoveAll(t => t == null);
                plots ??= new List<IntVec3>();
            }
        }
    }

    /// <summary>
    /// The field of the fallen (item 38): after a fight leaves a dozen dead or more lying on the colony's land, bell-keepers
    /// and sextons of the Quiet Bell camp three to six days, dig a row of graves beyond the wall and lay the dead in them, and
    /// ring the bell at each dusk. They ask for wood and cloth for the graves. Mourners of the dead's peoples come to stand at
    /// the graves (as the Bell's guests if their people are at war with the colony), and each of those peoples thinks better
    /// of the colony. It can turn, once a stay each: with Anomaly and the void awake, the dead still lying out rise (a fight,
    /// only while Rimfolk's world events may bring one; the gravewardens of item 35 may come to it), or the dead's people come
    /// for them: a people at peace with the colony asks at the gate (let them take their dead home, talk them into leaving
    /// them with the Bell, or say the Bell keeps them), one at war with the colony and the Bell sends a band against the camp
    /// (the camps' own, Camps/CampRaid.cs).
    /// </summary>
    public static class FieldOfFallen
    {
        public const string ClaimEvent = "Rimfolk_Evt_FallenClaimed";

        public static CampKindDef Kind => DefDatabase<CampKindDef>.GetNamedSilentFail("Rimfolk_Camp_FieldOfFallen");

        private static int Now => Find.TickManager.TicksGame;

        public static Camp On(Map map) => CampTracker.VisitorsAt(map).FirstOrDefault(c => c.kind == Kind);

        public static FallenState StateOf(Camp c) => c?.workState as FallenState;

        /// <summary>A body lying unburied on the land: humanlike, not the colony's own, seen.</summary>
        public static bool Lying(Thing t) =>
            t is Corpse c && !c.Destroyed && c.Spawned && c.InnerPawn?.RaceProps?.Humanlike == true && c.InnerPawn.Faction != Faction.OfPlayer && !c.Position.Fogged(c.Map);

        /// <summary>The dead lying on the land, the nearest the colony first.</summary>
        public static List<Thing> Dead(Map map)
        {
            if (map == null) return new List<Thing>();
            var mid = CampSite.ColonyCenter(map);
            return map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse).Where(Lying).OrderBy(c => c.Position.DistanceToSquared(mid)).ToList();
        }

        /// <summary>The camp made where a camp may stand, its people walking in from the edge; with <paramref name="gifts"/> the wood and cloth already sent.</summary>
        public static Camp Start(HappeningContext ctx, bool gifts)
        {
            var map = ctx?.map;
            var tracker = CampTracker.Instance;
            var kind = Kind;
            if (map == null || tracker == null || kind == null || ctx.faction == null || !CampTracker.Enabled) return null;
            try
            {
                var usual = CampWeather.StayRange(map, GenDate.TicksPerDay * 5);
                bool hot = CampWeatherMath.PitchForHeat(usual.high);
                if (!CampSite.TryFind(map, out var center, hot)) return null;
                var setup = tracker.Make(map, center, CampSite.ColonyCenter(map), kind, ctx.faction, hot, null, null);
                if (setup == null) return null;
                setup.land = TravelLand.Read(map);
                var c = tracker.Start(setup);
                if (c == null)
                {
                    CampTracker.Discard(setup);
                    return null;
                }
                c.workState = new FallenState { gifts = gifts, dead = Dead(map).Take(PeopleEvents5Math.FallenGraves).ToList() };
                return c;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 5: the field of the fallen", e);
                return null;
            }
        }

        /// <summary>The row of graves laid out beyond the wall, a grave each for the dead still lying out.</summary>
        public static void Plan(Camp c, FallenState st)
        {
            st.planned = true;
            st.dead.RemoveAll(t => !Lying(t));
            if (st.dead.Count == 0) return;
            st.plots = PlayBurial.Plot(c.map, c.leader, Math.Min(st.dead.Count, PeopleEvents5Math.FallenGraves), true, out var rot);
            st.rot = rot.AsInt;
        }

        /// <summary>A grave dug at its place in the row (the game's own grave, the colony's).</summary>
        public static void Dug(Pawn by, IntVec3 cell)
        {
            var c = CampTracker.VisitorsAt(by?.Map).FirstOrDefault(x => x.kind == Kind && x.Has(by));
            var st = StateOf(c);
            if (st == null || !st.plots.Contains(cell) || PlayBurial.GraveAt(c.map, cell) != null) return;
            if (Callers.DigNow(c.map, cell, new Rot4(st.rot)) != null && Rand.Chance(0.3f)) c.SayMoment(by, "dig");
        }

        /// <summary>One of the dead laid in a grave of the row.</summary>
        public static void Buried(Pawn by, Building_Grave grave)
        {
            var c = CampTracker.VisitorsAt(by?.Map).FirstOrDefault(x => x.kind == Kind && x.Has(by));
            var st = StateOf(c);
            var held = by?.carryTracker?.CarriedThing;
            if (st == null || held == null || grave == null || grave.HasAnyContents) return;
            if (!grave.TryAcceptThing(held, true)) return;
            st.dead.Remove(held);
            st.buried++;
            if (Rand.Chance(0.4f)) c.SayMoment(by, "bury");
        }

        /// <summary>The dusk's bell rung at the row (the bell-keeper's walk there done).</summary>
        public static void Rang(Pawn by, IntVec3 cell)
        {
            var c = CampTracker.VisitorsAt(by?.Map).FirstOrDefault(x => x.kind == Kind && x.Has(by));
            if (c == null) return;
            PeopleEvents5.Bell(c.map, cell);
            c.SayMoment(by, "ring");
        }

        private static bool OnIt(Pawn p) => p?.CurJob != null && (p.CurJobDef == PeopleEvents5.DigJob || p.CurJobDef == PeopleEvents5.BuryJob);

        /// <summary>One of theirs by day: a body into a grave dug and empty, else the next grave of the row while there are dead without one.</summary>
        public static Job Work(Camp c, FallenState st, Pawn p)
        {
            var map = c.map;
            if (!st.planned || st.plots.Count == 0 || !p.RaceProps.Humanlike) return null;
            var busy = c.Here.Where(o => o != p && OnIt(o)).Select(o => o.CurJob).ToList();
            var bodies = new HashSet<Thing>(busy.Where(j => j.def == PeopleEvents5.BuryJob).Select(j => j.targetA.Thing));
            var graves = new HashSet<Thing>(busy.Where(j => j.def == PeopleEvents5.BuryJob).Select(j => j.targetB.Thing));
            var digging = new HashSet<IntVec3>(busy.Where(j => j.def == PeopleEvents5.DigJob).Select(j => j.targetA.Cell));
            st.dead.RemoveAll(t => t == null || t.Destroyed || t.ParentHolder is Building_Grave);
            var grave = st.plots.Select(x => PlayBurial.GraveAt(map, x)).FirstOrDefault(g => g != null && !g.HasAnyContents && !graves.Contains(g) && p.CanReach(g, PathEndMode.Touch, Danger.Some));
            var body = st.dead.Where(t => Lying(t) && !bodies.Contains(t) && p.CanReserveAndReach(t, PathEndMode.ClosestTouch, Danger.Some))
                .OrderBy(t => t.Position.DistanceToSquared(p.Position)).FirstOrDefault();
            if (grave != null && body != null && PeopleEvents5.BuryJob != null)
            {
                var bury = JobMaker.MakeJob(PeopleEvents5.BuryJob, body, grave);
                bury.count = 1;
                return bury;
            }
            int empty = st.plots.Count(x => PlayBurial.GraveAt(map, x) is Building_Grave g && !g.HasAnyContents);
            if (empty + digging.Count >= st.dead.Count(Lying)) return null;
            var rot = new Rot4(st.rot);
            foreach (var x in st.plots)
                if (PlayBurial.GraveAt(map, x) == null && !digging.Contains(x) && GenConstruct.CanPlaceBlueprintAt(ThingDefOf.Grave, x, rot, map).Accepted
                    && p.CanReach(x, PathEndMode.Touch, Danger.Some))
                    return PeopleEvents5.Make(PeopleEvents5.DigJob, x, 900);
            return null;
        }

        /// <summary>
        /// Once the camp has stood a day and a half: mourners of the dead's peoples (three peoples at most) walk in to stand at
        /// the graves, a people at war with the colony as the Bell's guests; each people thinks better of the colony.
        /// </summary>
        public static void Mourners(Camp c, FallenState st)
        {
            st.mourners = true;
            var map = c.map;
            var at = st.plots.FirstOrDefault(x => PlayBurial.GraveAt(map, x) != null);
            if (!at.IsValid || at == IntVec3.Zero && PlayBurial.GraveAt(map, at) == null) at = c.FireCell;
            var peoples = map.listerBuildings.allBuildingsColonist.OfType<Building_Grave>().Where(g => st.plots.Contains(g.Position) && g.Corpse != null)
                .Select(g => g.Corpse.InnerPawn?.Faction).Where(f => OtherSideMourners.MayCome(f) && !f.HostileTo(c.faction)).GroupBy(f => f)
                .OrderByDescending(g => g.Count()).Take(PeopleEvents5Math.FallenPeoples).ToList();
            var came = new List<string>();
            foreach (var g in peoples)
            {
                var f = g.Key;
                if (!Arrivals.TryEntryCell(map, out var entry)) break;
                var list = new List<Pawn>();
                try
                {
                    for (int i = 0; i < 2; i++)
                    {
                        var m = Arrivals.Make(Arrivals.PersonKind(f), f, new FloatRange(25f, 70f));
                        if (m == null || !Arrivals.Spawn(m, map, entry)) continue;
                        if (f.HostileTo(Faction.OfPlayer)) m.guest?.SetGuestStatus(c.faction);
                        list.Add(m);
                    }
                    if (list.Count == 0) continue;
                    LordMaker.MakeNewLord(f, new LordJob_VisitOnErrand(f, at, 5 * GenDate.TicksPerHour), map, list);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("peoples' events 5: the field's mourners", e);
                    continue;
                }
                WorldSituationWorker.Goodwill(f, PeopleEvents5Math.FallenGoodwill(g.Count()), GoodwillWhy.TheirDead);
                FactionMemory.Note(f, "mourned");
                came.Add(f.Name);
            }
            if (came.Count > 0)
                c.Note(c.Text("CC_PEv5_FallenMourners", c.leader, null, came.Count, TextUtil.JoinAnd(came)), MessageTypeDefOf.PositiveEvent, Importance.Notable, null, true);
        }

        /// <summary>
        /// Once a stay, at night, with Anomaly and the void awake: the dead still lying out rise, at a small chance, only while
        /// Rimfolk's world events may bring a fight; told as a threat.
        /// </summary>
        public static void Stir(Camp c, FallenState st)
        {
            st.stirRolled = true;
            if (!ModsConfig.AnomalyActive || !IncidentWorker_Happening.BattlesAllowed(c.map)) return;
            int level;
            try
            {
                level = Find.Anomaly?.Level ?? 0;
            }
            catch
            {
                level = 0;
            }
            if (level <= 0 || !Rand.Chance(PeopleEvents5Math.FieldStirChance)) return;
            var rise = st.dead.OfType<Corpse>().Where(x => Lying(x) && MutantUtility.CanResurrectAsShambler(x, false)).Take(3).ToList();
            if (rise.Count == 0) return;
            var risen = new List<Pawn>();
            foreach (var body in rise)
                try
                {
                    var p = body.InnerPawn;
                    MutantUtility.ResurrectAsShambler(p, -1, Faction.OfEntities);
                    if (p.Spawned && !p.Dead) risen.Add(p);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("peoples' events 5: the field stirs", e);
                }
            if (risen.Count == 0) return;
            st.stirred = true;
            st.dead.RemoveAll(t => t.Destroyed || !(t is Corpse));
            var keeper = c.Here.FirstOrDefault(p => p.RaceProps.Humanlike);
            if (keeper != null) c.SayMoment(keeper, "stirs");
            Tell.Letter(NewsKind.Land, Importance.Important, c.map, "CC_PEv5_FieldStirsLabel".Translate().Resolve(), c.Text("CC_PEv5_FieldStirs", keeper, null, risen.Count),
                LetterDefOf.ThreatSmall, new LookTargets(risen));
        }

        /// <summary>
        /// Once a stay, after the first day: the dead's people come for them. A people at peace with the colony asks at the gate
        /// (their own event); one at war with the colony and the Bell sends a band against the camp (the camps' own).
        /// </summary>
        public static void Claim(Camp c, FallenState st)
        {
            st.claimRolled = true;
            if (!Rand.Chance(PeopleEvents5Math.ClaimChance)) return;
            var lying = st.dead.Where(Lying).Select(t => ((Corpse)t).InnerPawn.Faction).Where(f => f != null && !f.IsPlayer && !f.defeated && f.def.humanlikeFaction)
                .GroupBy(f => f).OrderByDescending(g => g.Count()).Select(g => g.Key).ToList();
            foreach (var f in lying)
            {
                if (!f.HostileTo(Faction.OfPlayer))
                {
                    if (Arrivals.PersonKind(f) == null || !Tell.CanAsk) continue;
                    st.claimPeople = f;
                    if (PeopleEvents2.Fire(ClaimEvent, c.map, f)) return;
                    st.claimPeople = null;
                }
                else if (f.HostileTo(c.faction) && CampRaids.Send(c, f, false, true) != null)
                {
                    st.claimPeople = f;
                    st.claimed = true;
                    return;
                }
            }
        }

        /// <summary>The dead's people's answer played out: <paramref name="how"/> "give" (they carry their dead off), "talked" (they leave them with the Bell), "refused" (taken anyway, or a band against the camp).</summary>
        public static void Claimed(HappeningContext ctx, string how)
        {
            var c = On(ctx?.map);
            var st = StateOf(c);
            var f = ctx?.faction;
            var band = PeoplesG.Party(ctx).Where(p => PeoplesG.Up(p, ctx.map)).ToList();
            if (st == null || f == null) return;
            st.claimed = true;
            switch (how)
            {
                case "give":
                    if (f.HasGoodwill && !f.IsPlayer) WorldSituationWorker.Goodwill(f, PeopleEvents5Math.ClaimGiveGoodwill, GoodwillWhy.TheirDead);
                    TakeHome(st, f, band);
                    return;
                case "talked":
                    if (f.HasGoodwill && !f.IsPlayer) WorldSituationWorker.Goodwill(f, PeopleEvents5Math.ClaimKeptGoodwill, GoodwillWhy.TheirDead);
                    PeoplesG.Home(band);
                    return;
            }
            if (f.HasGoodwill && !f.IsPlayer) WorldSituationWorker.Goodwill(f, PeopleEvents5Math.ClaimRefusedGoodwill, GoodwillWhy.TurnedAway);
            if (f.HostileTo(c.faction) && CampRaids.Send(c, f, false, true) != null)
            {
                PeoplesG.Home(band);
                return;
            }
            TakeHome(st, f, band);
        }

        /// <summary>Their band carries their dead still lying out off the land, one each, the game's own taking-away.</summary>
        private static void TakeHome(FallenState st, Faction f, List<Pawn> band)
        {
            var theirs = st.dead.Where(t => Lying(t) && ((Corpse)t).InnerPawn.Faction == f).ToList();
            foreach (var p in band)
            {
                var body = theirs.Where(t => p.CanReserveAndReach(t, PathEndMode.ClosestTouch, Danger.Some)).OrderBy(t => t.Position.DistanceToSquared(p.Position)).FirstOrDefault();
                if (body == null || !RCellFinder.TryFindBestExitSpot(p, out var exit, TraverseMode.ByPawn, false)) continue;
                theirs.Remove(body);
                st.dead.Remove(body);
                try
                {
                    Lords.Leave(p);
                    var j = JobMaker.MakeJob(JobDefOf.Steal, body, exit);
                    j.count = 1;
                    j.locomotionUrgency = LocomotionUrgency.Walk;
                    p.jobs.StartJob(j, JobCondition.InterruptForced, null, false, true);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("peoples' events 5: the dead carried home", e);
                }
            }
            PeoplesG.Home(band.Where(p => p.CurJobDef != JobDefOf.Steal));
        }
    }

    /// <summary>The field of the fallen's camp: the row of graves dug and filled, the dusk's bell, the mourners, and how it can turn.</summary>
    public class CampWorker_FieldOfFallen : CampWorker
    {
        public override void Started(Camp c)
        {
            if (!(c.workState is FallenState)) c.workState = new FallenState();
        }

        public override void Hourly(Camp c, int hour)
        {
            if (!(c.workState is FallenState st))
            {
                Started(c);
                st = FieldOfFallen.StateOf(c);
            }
            if (!c.Settled) return;
            if (!st.planned) FieldOfFallen.Plan(c, st);
            int day = GenDate.DaysPassed;
            if (PeopleEvents5Math.Dusk(hour) && st.rungDay != day)
            {
                st.rungDay = day;
                var keeper = c.Here.Where(p => p.RaceProps.Humanlike).OrderBy(p => p == c.leader ? 0 : 1).FirstOrDefault();
                var at = st.plots.FirstOrDefault(x => PlayBurial.GraveAt(c.map, x) != null);
                if (keeper != null && (!at.IsValid || PlayBurial.GraveAt(c.map, at) == null || !PeopleEvents5.Send(keeper, PeopleEvents5.RingJob, at, 600)))
                    FieldOfFallen.Rang(keeper, keeper.Position);
            }
            if (!st.mourners && c.DaysHere >= PeopleEvents5Math.MournersDay && st.buried > 0) FieldOfFallen.Mourners(c, st);
            if (!st.stirRolled && c.DaysHere >= 1f && (hour >= 22 || hour <= 3) && st.dead.Any(FieldOfFallen.Lying)) FieldOfFallen.Stir(c, st);
            if (!st.claimRolled && c.DaysHere >= 1f && hour >= 9 && hour <= 16 && !MapFacts.ThreatOn(c.map)) FieldOfFallen.Claim(c, st);
        }

        /// <summary>By day, the graves: digging, carrying the dead to them.</summary>
        public override Job DayJob(Camp c, Pawn p)
        {
            var st = FieldOfFallen.StateOf(c);
            return st == null ? null : FieldOfFallen.Work(c, st, p);
        }

        /// <summary>Going: their thanks when the dead were seen to.</summary>
        public override void Leaving(Camp c)
        {
            var st = FieldOfFallen.StateOf(c);
            if (st == null || c.Hostile || c.faction == null || !c.faction.HasGoodwill || c.faction.IsPlayer) return;
            if (st.buried > 0 && c.tension < 50f) WorldSituationWorker.Goodwill(c.faction, 2, GoodwillWhy.Helped);
            st.dead.Clear();
        }

        public override void AddMarks(Camp c, HashSet<string> marks)
        {
            var st = FieldOfFallen.StateOf(c);
            if (st == null) return;
            if (st.buried > 0) marks.Add("buried");
            if (st.stirred) marks.Add("stirred");
            if (st.claimed) marks.Add("claimed");
        }

        public override string AskBlocked(Camp c, CampAsk a)
        {
            var st = FieldOfFallen.StateOf(c);
            if (st == null) return null;
            switch (a?.key)
            {
                case "wood":
                case "cloth":
                    return st.gifts ? "CC_PEv5_FallenSent".Translate().Resolve() : null;
            }
            return null;
        }
    }

    /// <summary>Bell-keepers of {FACTION} ({X}) at the gate: {COUNT} dead lie on the colony's land, and they would camp and bury them.</summary>
    public class HappeningWorker_FieldOfFallen : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            FieldOfFallen.Kind != null && CampTracker.Enabled && CampTracker.RoomFor(map) && PeoplesD.Friendly(PeoplesMath.Sextons) != null && FieldOfFallen.On(map) == null
            && !MapFacts.ThreatOn(map) && FieldOfFallen.Dead(map).Count >= PeopleEvents5Math.FallenMin && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Sextons);
            if (ctx.faction == null) return false;
            var dead = FieldOfFallen.Dead(ctx.map);
            if (dead.Count < PeopleEvents5Math.FallenMin) return false;
            ctx.count = dead.Count;
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_QuietBell_Keeper"), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            ctx.a = KinMemory.Greeter(ctx.faction, ctx.map) ?? PeoplesD.Able(ctx.map).FirstOrDefault();
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            PeopleEvents.AgeTags(tags, ctx.faction);
            if (ModsConfig.AnomalyActive) tags.Add("anomaly");
        }

        public override string Unavailable(HappeningContext ctx, int option) =>
            option <= 1 && (FieldOfFallen.On(ctx.map) != null || !CampTracker.RoomFor(ctx.map)) ? "CC_PEv2_NoRoomCamp".Translate().Resolve() : null;

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option <= 1 ? "camp" : "ourselves");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The bell-keepers camp and dig (FieldOfFallen.Start); <see cref="gifts"/>: the wood and cloth sent out with them now.</summary>
    public class DilemmaEffect_FallenCamp : DilemmaEffect
    {
        public bool gifts;

        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc == null) return;
            sheet.Add(ImpactKind.People, 1, "CC_PEv5_Impact_Fallen".Translate(hc.faction?.Name ?? "?", Math.Min(hc.count, PeopleEvents5Math.FallenGraves)).Resolve(),
                "CC_PEv5_Impact_FallenBrief".Translate().Resolve(), "pev5fallen");
            if (gifts && hc.faction != null) sheet.Goodwill(hc.faction, 2);
        }

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            if (hc?.map == null) return;
            if (gifts && hc.faction != null && hc.faction.HasGoodwill && !hc.faction.IsPlayer) WorldSituationWorker.Goodwill(hc.faction, 2, GoodwillWhy.Gift);
            FieldOfFallen.Start(hc, gifts);
            Arrivals.SendAway(PeoplesG.Party(hc));
        }
    }

    /// <summary>
    /// {X} of {FACTION} at the gate with two of their people: {COUNT} of theirs lie in the field of the fallen, and they would
    /// take them home; {TALE} is the Bell keeping the field.
    /// </summary>
    public class HappeningWorker_FallenClaimed : HappeningWorker_Visitors
    {
        private static Camp Field(Map map, out FallenState st)
        {
            var c = FieldOfFallen.On(map);
            st = FieldOfFallen.StateOf(c);
            return c;
        }

        private static List<Thing> Theirs(FallenState st, Faction f) =>
            st == null || f == null ? new List<Thing>() : st.dead.Where(t => FieldOfFallen.Lying(t) && ((Corpse)t).InnerPawn.Faction == f).ToList();

        protected override bool CanHappen(Map map, HappeningExtension ext)
        {
            var c = Field(map, out var st);
            return c != null && st?.claimPeople != null && !st.claimed && !st.claimPeople.HostileTo(Faction.OfPlayer) && Theirs(st, st.claimPeople).Count > 0 && Colonists(map).Count > 0;
        }

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var c = Field(ctx.map, out var st);
            ctx.faction = st?.claimPeople;
            if (c == null || ctx.faction == null) return false;
            ctx.count = Theirs(st, ctx.faction).Count;
            ctx.tale = c.faction?.Name ?? "?";
            for (int i = 0; i < 3; i++)
            {
                var p = Arrivals.Make(Arrivals.PersonKind(ctx.faction), ctx.faction, ext.ages);
                if (p == null) continue;
                if (ctx.stranger == null) ctx.stranger = p;
                else ctx.group.Add(p);
            }
            if (ctx.stranger == null) return false;
            ctx.a = PeoplesD.Best(ctx.map, SkillDefOf.Social) ?? PeoplesD.Able(ctx.map).FirstOrDefault();
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            var c = Field(ctx.map, out _);
            if (c?.faction != null && ctx.faction != null && ctx.faction.HostileTo(c.faction)) tags.Add("feud");
        }

        /// <summary>Talked out of it: the colony's talker's Social (PeopleEvents5Math.ClaimTalkFails).</summary>
        public override float RiskChance(HappeningContext ctx, int option, float chance) =>
            option == 1 ? PeopleEvents5Math.ClaimTalkFails(ctx.a?.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0) : base.RiskChance(ctx, option, chance);

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option == 0 ? "home" : option == 1 && !risk ? "talked" : "refused");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The dead's people's answer (FieldOfFallen.Claimed): <see cref="how"/> "give", "talked", "refused".</summary>
    public class DilemmaEffect_FallenClaim : DilemmaEffect
    {
        public string how = "give";

        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var f = ctx?.faction;
            switch (how)
            {
                case "give":
                    sheet.Play("CC_PEv5_Impact_ClaimGive".Translate(f?.Name ?? "?").Resolve(), "CC_PEv5_Impact_ClaimGiveBrief".Translate().Resolve());
                    if (f != null) sheet.Goodwill(f, PeopleEvents5Math.ClaimGiveGoodwill);
                    break;
                case "talked":
                    if (f != null) sheet.Goodwill(f, PeopleEvents5Math.ClaimKeptGoodwill);
                    break;
                default:
                    if (f != null) sheet.Goodwill(f, PeopleEvents5Math.ClaimRefusedGoodwill);
                    sheet.Add(ImpactKind.Other, -1, "CC_PEv5_Impact_ClaimRefused".Translate(f?.Name ?? "?").Resolve(), "CC_PEv5_Impact_ClaimRefusedBrief".Translate().Resolve(), "pev5claimrefused");
                    break;
            }
        }

        public override void Apply(DilemmaContext ctx) => FieldOfFallen.Claimed(ctx as HappeningContext, how);
    }
}
