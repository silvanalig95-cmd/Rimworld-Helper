using System;
using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    // Release F's Last Republic and Hollow Fleet at the colony's gate (docs/design/peoples.md, "Release F"; the defs in
    // Defs/IncidentDefs/Rimfolk_Events_PeoplesF.xml; the Signal Line and the Deepwell Union are in PeoplesF.cs): the census
    // walked through the colony, citizenship papers, a conscript's season and the Republic's radio; the Fleet's shuttle,
    // its salvage buyer and the sleeper woken too late; and the dev actions of all four. Nothing saved of its own: the
    // papers and the conscript ride on the events' visits, the radio on the region's day.

    // ===================== The Last Republic: the census =====================

    /// <summary>
    /// A census-taker of the Last Republic, a guard beside them, comes to count the colony's people: walked from bed to
    /// bed (or to whoever has none, where they stand) with the census roll, a while at each, then the count written and
    /// told, and the Republic thinks the better of the colony. The colony's best talker may walk them round. Or the colony
    /// keeps its own count, and the Republic thinks a little the worse. {COUNT} is how many there are to count.
    /// </summary>
    public class HappeningWorker_RepublicCensus : HappeningWorker_Visitors
    {
        /// <summary>The colony's people at home to count: grown and young, the colony's own.</summary>
        public static List<Pawn> Counted(Map map) => map == null ? new List<Pawn>() : map.mapPawns.FreeColonistsSpawned.Where(p => !p.Dead).ToList();

        /// <summary>Where each of them is counted: their own bed, else where they stand now.</summary>
        public static List<LocalTargetInfo> Stops(Map map) =>
            Counted(map).Select(p => p.ownership?.OwnedBed is Building_Bed b && b.Spawned && b.Map == map ? (LocalTargetInfo)b.Position : (LocalTargetInfo)p.Position)
                .Distinct().ToList();

        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            PeoplesD.Friendly(PeoplesMath.Citizens) != null && Counted(map).Count >= 2 && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Citizens);
            if (ctx.faction == null) return false;
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Republic_Clerk"), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            var guard = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Republic_Guard"), ctx.faction, ext.ages);
            if (guard != null) ctx.group.Add(guard);
            ctx.count = Counted(ctx.map).Count;
            ctx.a = PeoplesD.Best(ctx.map, SkillDefOf.Social) ?? PeoplesD.Able(ctx.map).RandomElementWithFallback();
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(ctx.group.Prepend(ctx.stranger).ToList(), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            if (Papers.Holds(ctx.faction)) tags.Add("citizens");
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesF.Say(ctx.stranger, Ext, ctx, option <= 1 ? "count" : "refused");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The census walked: the census-taker goes from stop to stop with the roll (Census.Counted); with the play-outs off, written at once.</summary>
    public class DilemmaEffect_RepublicCensus : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            sheet.Play("CC_PeoplesF_Impact_Census".Translate(ctx?.count ?? 0).Resolve(), "CC_PeoplesF_Impact_CensusBrief".Translate().Resolve());
            var f = ctx?.faction;
            if (f != null && f.HasGoodwill && !f.IsPlayer) sheet.Goodwill(f, PeoplesFMath.CountedGoodwill);
        }

        public override void Apply(DilemmaContext ctx)
        {
            var map = ctx?.map;
            if (map == null || ctx.faction == null) return;
            var x = ctx.stranger;
            var stops = HappeningWorker_RepublicCensus.Stops(map).Where(s => x != null && x.Spawned && x.CanReach(s, PathEndMode.Touch, Danger.Some)).ToList();
            if (PlayOuts.On && PeoplesF.Up(x, map) && stops.Count > 0)
            {
                Census.Begin(x, ctx.faction, (ctx as HappeningContext)?.incident);
                Lords.Leave(x);
                LordMaker.MakeNewLord(x.Faction, new LordJob_VisitOnErrand(x.Faction, PeoplesD.GateSpot(map) ?? stops[0].Cell, 6 * GenDate.TicksPerHour), map, new List<Pawn> { x });
                if (PeoplesE.Start(x, PeoplesF.CountJob, stops[0], stops.Skip(1).ToList(), PeoplesFMath.CensusTicks)) return;
            }
            Census.Written(x, map, ctx.faction);
        }
    }

    /// <summary>The census under way: each stop counted (a line over the census-taker's head), and the count written at the last.</summary>
    public static class Census
    {
        private sealed class Count
        {
            public Faction people;
            public IncidentDef incident;
        }

        private static readonly Dictionary<int, Count> counts = new Dictionary<int, Count>();

        public static void Begin(Pawn taker, Faction f, IncidentDef incident)
        {
            if (taker != null) counts[taker.thingIDNumber] = new Count { people = f, incident = incident };
        }

        public static void Counted(Pawn taker, IntVec3 at, bool last)
        {
            if (taker == null) return;
            counts.TryGetValue(taker.thingIDNumber, out var c);
            if (!last)
            {
                if (Rand.Chance(0.5f)) PeoplesF.SayLine(taker, c?.incident, "stop");
                return;
            }
            counts.Remove(taker.thingIDNumber);
            Written(taker, taker.MapHeld, c?.people ?? taker.Faction);
        }

        /// <summary>The count written into the Republic's roll: told with every name, and the Republic thinks the better of the colony.</summary>
        public static void Written(Pawn taker, Map map, Faction f)
        {
            if (map == null || f == null) return;
            var people = HappeningWorker_RepublicCensus.Counted(map);
            if (f.HasGoodwill && !f.IsPlayer) WorldSituationWorker.Goodwill(f, PeoplesFMath.CountedGoodwill);
            string names = TextUtil.JoinAnd(people.Select(p => p.LabelShort).ToList());
            Tell.Letter(NewsKind.World, Importance.Notable, map, "CC_PeoplesF_CensusLabel".Translate(f.Name).Resolve(),
                "CC_PeoplesF_CensusWritten".Translate(f.Name.Named("FACTION"), people.Count.Named("COUNT"), names.Named("NAMES"), (taker?.LabelShort ?? "?").Named("TAKER")).Resolve(),
                LetterDefOf.NeutralEvent, taker != null && taker.Spawned ? new LookTargets(taker) : LookTargets.Invalid, f);
            if (taker != null && taker.Spawned && taker.Faction != null && !taker.Faction.IsPlayer)
            {
                var crew = map.mapPawns.AllPawnsSpawned.Where(p => p.Faction == taker.Faction && p.GetLord() != null && p.HostFaction == null && !p.Dead).ToList();
                Arrivals.SendAway(crew.Contains(taker) ? crew : crew.Append(taker));
            }
        }
    }

    /// <summary>The colony keeps its own count: the Republic hears that it was turned away.</summary>
    public class DilemmaEffect_RepublicUncounted : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var f = ctx?.faction;
            if (f != null && f.HasGoodwill && !f.IsPlayer) sheet.Goodwill(f, PeoplesFMath.UncountedGoodwill);
        }

        public override void Apply(DilemmaContext ctx)
        {
            var f = ctx?.faction;
            if (f != null && f.HasGoodwill && !f.IsPlayer) WorldSituationWorker.Goodwill(f, PeoplesFMath.UncountedGoodwill);
        }
    }

    // ===================== The Last Republic: citizenship papers =====================

    /// <summary>
    /// The colony's citizenship papers of the Last Republic: a visit of the events' tracker (kind "republicPapers") for a
    /// year, its text the Republic's load id, done when they are struck. While they hold, the Republic's traders give the
    /// colony a citizen's terms (the game's own price improvement, PeoplesFMath.PapersTerms more: Patch_PapersPawnTerms,
    /// Patch_PapersTownTerms), and the colony owes the Republic its duty: when it asks for a conscript, refusing strikes the
    /// papers (DilemmaEffect_RepublicRefused).
    /// </summary>
    public static class Papers
    {
        public const string Kind = "republicPapers";

        public static Visit Of(Faction f)
        {
            if (f == null) return null;
            string id = f.GetUniqueLoadID();
            return PeoplesF.Running(Kind).FirstOrDefault(v => v.text == id);
        }

        public static bool Holds(Faction f) => f != null && Peoples.CallingOf(f) == PeoplesMath.Citizens && Of(f) != null;

        /// <summary>The terms the colony has with a trader of this people: a citizen's, or none.</summary>
        public static float Terms(Faction f) => Holds(f) ? PeoplesFMath.PapersTerms : 0f;

        public static void Grant(Faction f, Map map)
        {
            var t = CouncilGameComponent.Instance?.happenings;
            if (t == null || f == null || map == null || Holds(f)) return;
            int now = PeoplesF.Now;
            var v = t.AddVisit(Kind, null, map, null, null, null, now + PeoplesFMath.PapersDays * GenDate.TicksPerDay);
            v.text = f.GetUniqueLoadID();
            v.due = now;
            if (f.HasGoodwill && !f.IsPlayer) WorldSituationWorker.Goodwill(f, PeoplesFMath.PapersGoodwill);
            Tell.Message(NewsKind.World, Importance.Notable, map, "CC_PeoplesF_PapersGranted".Translate(f.Name, PeoplesFMath.PapersTerms.ToStringPercent()).Resolve(), LookTargets.Invalid,
                MessageTypeDefOf.PositiveEvent);
        }

        /// <summary>The duty refused: the papers struck, told.</summary>
        public static void Strike(Faction f, Map map)
        {
            var v = Of(f);
            if (v == null) return;
            v.done = true;
            if (f.HasGoodwill && !f.IsPlayer) WorldSituationWorker.Goodwill(f, PeoplesFMath.StruckGoodwill);
            Tell.Message(NewsKind.World, Importance.Notable, map, "CC_PeoplesF_PapersStruck".Translate(f.Name).Resolve(), LookTargets.Invalid, MessageTypeDefOf.NegativeEvent);
        }

        /// <summary>The papers' hour: they lapse with their year, told; gone with the Republic.</summary>
        public static bool Hour(Visit v, CouncilGameComponent comp, int now)
        {
            if (v.done) return false;
            var f = PeoplesF.PeopleOf(v);
            if (f == null || f.defeated) return false;
            if (now <= v.until) return true;
            Tell.Message(NewsKind.World, Importance.Flavor, v.map, "CC_PeoplesF_PapersLapsed".Translate(f.Name).Resolve(), LookTargets.Invalid, MessageTypeDefOf.NeutralEvent);
            return false;
        }
    }

    /// <summary>
    /// A clerk of the Last Republic brings the colony citizenship papers: a citizen's terms with the Republic's traders, and
    /// a duty owed. Taken (the colony's best talker swearing for it, if it likes), they hold a year. Or declined.
    /// </summary>
    public class HappeningWorker_RepublicPapers : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext)
        {
            var f = PeoplesD.Friendly(PeoplesMath.Citizens);
            return f != null && !Papers.Holds(f) && Colonists(map).Count > 0;
        }

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = Arrivals.Friendly(f => PeoplesD.Theirs(f, PeoplesMath.Citizens) && !Papers.Holds(f));
            if (ctx.faction == null) return false;
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Republic_Clerk"), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            ctx.tale = PeoplesFMath.PapersTerms.ToStringPercent();
            ctx.a = PeoplesD.Best(ctx.map, SkillDefOf.Social) ?? PeoplesD.Able(ctx.map).RandomElementWithFallback();
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(new List<Pawn> { ctx.stranger }, ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesF.Say(ctx.stranger, Ext, ctx, option <= 1 ? "papers" : "thanks");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The papers taken: a year of a citizen's terms, and the duty (Papers.Grant).</summary>
    public class DilemmaEffect_RepublicPapers : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            sheet.Play("CC_PeoplesF_Impact_Papers".Translate(PeoplesFMath.PapersTerms.ToStringPercent(), PeoplesFMath.PapersDays).Resolve(),
                "CC_PeoplesF_Impact_PapersBrief".Translate().Resolve());
            var f = ctx?.faction;
            if (f != null && f.HasGoodwill && !f.IsPlayer) sheet.Goodwill(f, PeoplesFMath.PapersGoodwill);
        }

        public override void Apply(DilemmaContext ctx) => Papers.Grant(ctx?.faction, ctx?.map);
    }

    // ===================== The Last Republic: a conscript for a season =====================

    /// <summary>
    /// A sergeant of the Civic Guard comes to the gate: the Republic asks the colony for one good fighter for a season of
    /// its service at its town ({PLACE}, {TALE} away). Sent, the conscript takes the Guard's rations, walks out and goes by
    /// the world's own roads as a caravan of their own (Conscripts); served, they come home with the pay, the drill and a
    /// citizen's thanks. Or the colony pays to be excused ({COUNT} silver), or refuses, and a colony with the Republic's
    /// papers has them struck for it.
    /// </summary>
    public class HappeningWorker_RepublicConscript : HappeningWorker_Visitors
    {
        /// <summary>The colony's grown people who could serve: up, able to fight, and never one of its last two grown-ups.</summary>
        public static List<Pawn> Candidates(Map map)
        {
            var able = PeoplesD.Able(map);
            if (able.Count(p => !p.DevelopmentalStage.Juvenile()) <= 2) return new List<Pawn>();
            return able.Where(p => !p.DevelopmentalStage.Juvenile() && !p.WorkTagIsDisabled(WorkTags.Violent) && p.GetCaravan() == null).ToList();
        }

        protected override bool CanHappen(Map map, HappeningExtension ext)
        {
            var f = PeoplesD.Friendly(PeoplesMath.Citizens);
            return f != null && Conscripts.Serving() == null && WorldSituationWorker.NearestTown(f, map) != null && Candidates(map).Count > 0 && Colonists(map).Count > 0;
        }

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Citizens);
            var town = ctx.faction == null ? null : WorldSituationWorker.NearestTown(ctx.faction, ctx.map);
            if (town == null) return false;
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Republic_Guard"), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            ctx.a = Candidates(ctx.map).OrderByDescending(p => (p.skills?.GetSkill(SkillDefOf.Shooting)?.Level ?? 0) + (p.skills?.GetSkill(SkillDefOf.Melee)?.Level ?? 0)).FirstOrDefault();
            if (ctx.a == null) return false;
            ctx.count = PeoplesFMath.Exemption(ctx.map.wealthWatcher.WealthTotal);
            ctx.placeName = town.Name;
            ctx.tale = TextUtil.DaysString(Mathf.RoundToInt(Conscripts.RoadDays(ctx.map, town) * GenDate.TicksPerDay));
            return true;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(new List<Pawn> { ctx.stranger }, ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            if (Papers.Holds(ctx.faction)) tags.Add("citizens");
        }

        public override string Unavailable(HappeningContext ctx, int option) =>
            option == 0 && (ctx.a == null || ctx.a.Dead || ctx.a.Downed || !ctx.a.Spawned || Conscripts.Serving() != null) ? "CC_PeoplesF_NoConscript".Translate().Resolve() : null;

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesF.Say(ctx.stranger, Ext, ctx, option == 0 ? "sent" : option == 1 ? "paid" : "refused");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The conscript sent (Conscripts.Send): a season of the Republic's service, the roads both ways the game's own.</summary>
    public class DilemmaEffect_RepublicConscript : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (ctx?.a == null) return;
            var hc = ctx as HappeningContext;
            sheet.Add(ImpactKind.People, -1, "CC_PeoplesF_Impact_Conscript".Translate(ctx.a.LabelShort, PeoplesFMath.ServiceDays, hc?.placeName ?? "?").Resolve(),
                "CC_PeoplesF_Impact_ConscriptBrief".Translate(ctx.a.LabelShort).Resolve(), "conscript");
            sheet.Play("CC_PeoplesF_Impact_ConscriptRoad".Translate(ctx.a.LabelShort).Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            var town = ctx?.faction == null ? null : WorldSituationWorker.NearestTown(ctx.faction, ctx.map);
            if (ctx?.a == null || town == null) return;
            Conscripts.Send(ctx.a, ctx.faction, town, ctx.map);
        }
    }

    /// <summary>The Republic's call refused: it thinks less of the colony, and the papers of a citizen colony are struck.</summary>
    public class DilemmaEffect_RepublicRefused : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var f = ctx?.faction;
            if (f == null || !f.HasGoodwill || f.IsPlayer) return;
            sheet.Goodwill(f, PeoplesFMath.RefusedGoodwill + (Papers.Holds(f) ? PeoplesFMath.StruckGoodwill : 0));
            if (Papers.Holds(f)) sheet.Add(ImpactKind.Other, -1, "CC_PeoplesF_Impact_Struck".Translate(f.Name).Resolve(), "CC_PeoplesF_Impact_StruckBrief".Translate().Resolve(), "struck");
        }

        public override void Apply(DilemmaContext ctx)
        {
            var f = ctx?.faction;
            if (f == null || !f.HasGoodwill || f.IsPlayer) return;
            WorldSituationWorker.Goodwill(f, PeoplesFMath.RefusedGoodwill);
            if (Papers.Holds(f)) Papers.Strike(f, ctx.map);
        }
    }

    /// <summary>
    /// The colony's conscript in the Republic's service: a visit of the events' tracker (kind "conscript"), x the colonist,
    /// its text "republic|town", its stake the stage (0 setting out, 1 on the road there, 2 at the town, 3 on the road home),
    /// its due the end of the season. They walk to the map's edge with the Guard's rations and set out as a caravan of
    /// their own on the game's roads (the envoys' way, Diplomacy/EnvoysAbroad.cs); at the town they serve the season,
    /// kept in the world; at its end (or sooner, with the town fallen or the Republic turned on the colony) they set out
    /// for home with the pay, by the same roads. Whether they come back is the world's: the roads' ambushes and
    /// weather are the game's own; nothing of Rimfolk's hurts them.
    /// </summary>
    public static class Conscripts
    {
        public const string Kind = "conscript";

        public const int SettingOut = 0, Going = 1, AtTown = 2, Coming = 3;

        /// <summary>The conscript serving now (any stage), or null.</summary>
        public static Visit Serving()
        {
            var all = PeoplesF.Visits;
            return all?.FirstOrDefault(v => v != null && v.kind == Kind && !v.done);
        }

        /// <summary>Days on the road to a town, roughly (a walking caravan of one).</summary>
        public static float RoadDays(Map home, Settlement town) =>
            home == null || town == null ? 2f : 0.5f + Find.WorldGrid.ApproxDistanceInTiles(home.Tile, town.Tile) * 0.4f;

        private static Settlement TownOf(Visit v)
        {
            var bits = v?.text?.Split('|');
            return bits == null || bits.Length < 2 || !int.TryParse(bits[1], out int id) ? null : Find.WorldObjects.Settlements.FirstOrDefault(s => s.ID == id);
        }

        /// <summary>Sent: the Guard's rations in their pack, a walk to the map's edge, the visit begun.</summary>
        public static bool Send(Pawn p, Faction f, Settlement town, Map home)
        {
            var t = CouncilGameComponent.Instance?.happenings;
            var job = PeoplesF.OutJob;
            if (t == null || p == null || f == null || town == null || home == null || job == null || !p.Spawned || Serving() != null) return false;
            if (!RCellFinder.TryFindBestExitSpot(p, out var edge, TraverseMode.ByPawn)) return false;
            var v = t.AddVisit(Kind, null, home, p, null, null, PeoplesF.Now + 90 * GenDate.TicksPerDay);
            v.text = f.GetUniqueLoadID() + "|" + town.ID;
            v.stake = SettingOut;
            WorldLedger.Keep(p, KeepFor.Lent, false);
            try
            {
                Hosting.Provision(p, RoadDays(home, town) + 1f);
                if (p.Drafted) p.drafter.Drafted = false;
                var walk = JobMaker.MakeJob(job, edge);
                walk.locomotionUrgency = Pace.Errand;
                p.jobs.StartJob(walk, JobCondition.InterruptForced, null, false, true);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples F: the conscript sets out", e);
            }
            Tell.Message(NewsKind.World, Importance.Important, home, "CC_PeoplesF_ConscriptSettingOut".Translate(p.Named("PAWN"), town.Name.Named("TOWN")).Resolve(), new LookTargets(p),
                MessageTypeDefOf.NeutralEvent);
            return true;
        }

        /// <summary>At the map's edge (JobDriver_ConscriptOut): a caravan of their own on the road to the town.</summary>
        public static void AtEdge(Pawn p)
        {
            var v = Serving();
            var town = TownOf(v);
            var map = v?.map;
            if (v == null || v.x != p || town == null || map == null || !p.Spawned) return;
            try
            {
                var c = CaravanExitMapUtility.ExitMapAndCreateCaravan(new List<Pawn> { p }, Faction.OfPlayer, map.Tile, map.Tile, PlanetTile.Invalid, false);
                c?.pather?.StartPath(town.Tile, null, true);
                v.stake = Going;
                Tell.Message(NewsKind.World, Importance.Notable, map, "CC_PeoplesF_ConscriptSetOut".Translate(p.Named("PAWN"), town.Name.Named("TOWN")).Resolve(),
                    c != null ? new LookTargets(c) : LookTargets.Invalid, MessageTypeDefOf.NeutralEvent);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples F: the conscript's caravan", e);
            }
        }

        /// <summary>The service's hour, stage by stage.</summary>
        public static bool Hour(Visit v, CouncilGameComponent comp, int now)
        {
            var p = v.x;
            var f = PeoplesF.PeopleOf(v);
            var town = TownOf(v);
            var map = v.map;
            if (p == null || p.Discarded)
            {
                End(v);
                return false;
            }
            if (p.Dead)
            {
                // Lost on the world's roads (the game's own ambush or weather): told, and the service ends.
                Tell.Letter(NewsKind.World, Importance.Important, map, "CC_PeoplesF_ConscriptLostLabel".Translate(p.LabelShort).Resolve(),
                    "CC_PeoplesF_ConscriptLost".Translate(p.Named("PAWN"), (f?.Name ?? "?").Named("FACTION")).Resolve(), LetterDefOf.Death, LookTargets.Invalid, f);
                End(v);
                return false;
            }
            switch (v.stake)
            {
                case SettingOut:
                    if (p.Spawned && p.CurJobDef != PeoplesF.OutJob)
                    {
                        // The player gave them other work before they reached the edge: they stayed.
                        Tell.Message(NewsKind.World, Importance.Notable, map, "CC_PeoplesF_ConscriptStayed".Translate(p.Named("PAWN")).Resolve(), new LookTargets(p), MessageTypeDefOf.NeutralEvent);
                        End(v);
                        return false;
                    }
                    return true;
                case Going:
                {
                    var c = p.GetCaravan();
                    if (c == null)
                    {
                        // Turned for home by the player, or carried off: the service is off.
                        if (p.Spawned || People.Kidnapped(p))
                        {
                            End(v);
                            return false;
                        }
                        return true;
                    }
                    if (town == null || town.Destroyed || f == null || f.HostileTo(Faction.OfPlayer))
                    {
                        End(v);
                        return false;
                    }
                    bool there = c.Tile == town.Tile || !c.pather.Moving && Find.WorldGrid.ApproxDistanceInTiles(c.Tile, town.Tile) <= 1.01f;
                    if (there) Arrived(v, c, town, f);
                    else if (!c.pather.Moving && !c.pather.Paused) c.pather.StartPath(town.Tile, null, true);
                    return true;
                }
                case AtTown:
                    if (town == null || town.Destroyed || f == null || f.defeated || f.HostileTo(Faction.OfPlayer) || now >= v.due) Home(v, town, f, map, now >= v.due);
                    return true;
                case Coming:
                    if (map != null && p.Spawned && p.Map == map) Back(v, f, map);
                    else if (p.GetCaravan() == null && !p.Spawned)
                    {
                        End(v);
                        return false;
                    }
                    else if (p.Spawned && Homes.IsHome(p.Map)) Back(v, f, p.Map);
                    return !v.done;
            }
            return false;
        }

        private static void Arrived(Visit v, Caravan c, Settlement town, Faction f)
        {
            var p = v.x;
            c.RemovePawn(p);
            if (!c.PawnsListForReading.Any(x => x != null && x.IsColonist)) c.Destroy();
            if (!Find.WorldPawns.Contains(p)) Find.WorldPawns.PassToWorld(p, PawnDiscardDecideMode.KeepForever);
            WorldLedger.Keep(p, KeepFor.Lent);
            v.stake = AtTown;
            v.due = PeoplesF.Now + PeoplesFMath.ServiceDays * GenDate.TicksPerDay;
            v.until = v.due + 60 * GenDate.TicksPerDay;
            Tell.Letter(NewsKind.World, Importance.Notable, v.map, "CC_PeoplesF_ConscriptThereLabel".Translate(p.LabelShort, town.Name).Resolve(),
                "CC_PeoplesF_ConscriptThere".Translate(p.Named("PAWN"), town.Name.Named("TOWN"), f.Name.Named("FACTION"), PeoplesFMath.ServiceDays.Named("DAYS")).Resolve(),
                LetterDefOf.NeutralEvent, new LookTargets(town), f);
        }

        /// <summary>The season served (or cut short): rations and, served, the pay in their pack, and a caravan of their own for home.</summary>
        private static void Home(Visit v, Settlement town, Faction f, Map map, bool served)
        {
            var p = v.x;
            bool paid = served && f != null && !f.HostileTo(Faction.OfPlayer);
            int pay = paid ? PeoplesFMath.ServicePay(Peoples.AgeOf(f)) : 0;
            PlanetTile from = town != null && !town.Destroyed ? town.Tile : p.Tile;
            try
            {
                Hosting.Provision(p, RoadDays(map, town) + 1f);
                if (pay > 0)
                    foreach (var t in Couriers.Make(ThingDefOf.Silver, pay))
                        p.inventory?.innerContainer.TryAdd(t);
                if (!Find.WorldPawns.Contains(p)) Find.WorldPawns.PassToWorld(p, PawnDiscardDecideMode.KeepForever);
                var c = CaravanMaker.MakeCaravan(new List<Pawn> { p }, Faction.OfPlayer, from, false);
                if (map?.Parent != null) c.pather.StartPath(map.Tile, new CaravanArrivalAction_Enter(map.Parent), true);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples F: the conscript's road home", e);
            }
            WorldLedger.Release(p, KeepFor.Lent);
            Find.WorldPawns.ForcefullyKeptPawns.Remove(p);
            v.stake = Coming;
            v.skill = paid ? SkillDefOf.Shooting : null;
            string why = paid ? "CC_PeoplesF_ConscriptServed" : town == null || town.Destroyed ? "CC_PeoplesF_ConscriptTownFell" : "CC_PeoplesF_ConscriptSentHome";
            Tell.Letter(NewsKind.World, Importance.Notable, map, "CC_PeoplesF_ConscriptHomewardLabel".Translate(p.LabelShort).Resolve(),
                why.Translate(p.Named("PAWN"), (town?.Name ?? f?.Name ?? "?").Named("TOWN"), (f?.Name ?? "?").Named("FACTION"), pay.Named("PAY")).Resolve(), LetterDefOf.NeutralEvent,
                p.GetCaravan() != null ? new LookTargets(p.GetCaravan()) : LookTargets.Invalid, f);
        }

        /// <summary>Home again: what the season taught, their pride in it, the Republic's thanks, told.</summary>
        private static void Back(Visit v, Faction f, Map map)
        {
            var p = v.x;
            bool served = v.skill != null;
            if (served)
            {
                try
                {
                    p.skills?.Learn(SkillDefOf.Shooting, PeoplesFMath.ServiceXp, true);
                    p.skills?.Learn(SkillDefOf.Melee, PeoplesFMath.ServiceXp * 0.5f, true);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("peoples F: the conscript's practice", e);
                }
                var thought = DefDatabase<ThoughtDef>.GetNamedSilentFail("Rimfolk_Mission_ServedRepublic");
                if (thought != null) CouncilGameComponent.GiveThought(p, thought);
                if (f != null && f.HasGoodwill && !f.IsPlayer) WorldSituationWorker.Goodwill(f, PeoplesFMath.ServedGoodwill);
            }
            Tell.Message(NewsKind.World, Importance.Notable, map, (served ? "CC_PeoplesF_ConscriptBack" : "CC_PeoplesF_ConscriptBackEarly").Translate(p.Named("PAWN"), (f?.Name ?? "?").Named("FACTION")).Resolve(),
                new LookTargets(p), MessageTypeDefOf.PositiveEvent);
            End(v);
        }

        private static void End(Visit v)
        {
            v.done = true;
            if (v.x != null) WorldLedger.Release(v.x, KeepFor.Lent);
        }

        /// <summary>The colony calls its conscript home before the season is out (the dev action, and a home lost).</summary>
        public static void Recall()
        {
            var v = Serving();
            if (v == null || v.stake != AtTown) return;
            Home(v, TownOf(v), PeoplesF.PeopleOf(v), v.map, true);
        }
    }

    /// <summary>The conscript walks to the map's edge (target A) and sets out as a caravan of their own for the Republic's town.</summary>
    public class JobDriver_ConscriptOut : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);
            var go = ToilMaker.MakeToil("ConscriptOut");
            go.initAction = () => Conscripts.AtEdge(pawn);
            go.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return go;
        }
    }

    // ===================== The Last Republic: its radio =====================

    /// <summary>
    /// Once a season, on each Republic's own day, its evening broadcast speaks of the colony when there is something to say
    /// (PeoplesFMath.Broadcast): praise for a colony of citizens or good friends, a denunciation for one it is cool with or
    /// at war with. Heard only at a home with a radio (a powered comms console, or the Signal Line's mast singing): told
    /// in the Republic's voice, and felt by everyone at home (Rimfolk_Thought_RadioPraised, _RadioDenounced).
    /// </summary>
    public static class RepublicRadio
    {
        public static bool HasRadio(Map map) =>
            map != null && (map.listerBuildings.ColonistsHaveBuildingWithPowerOn(ThingDefOf.CommsConsole) || RelayMast.Working(map));

        public static void Daily(int now)
        {
            int day = now / GenDate.TicksPerDay;
            var home = Find.Maps.Where(m => m.IsPlayerHome && HasRadio(m)).OrderByDescending(m => m.mapPawns.FreeColonistsSpawnedCount).FirstOrDefault();
            if (home == null) return;
            foreach (var f in Find.FactionManager.AllFactionsListForReading.Where(f => f != null && !f.defeated && !f.IsPlayer && Peoples.CallingOf(f) == PeoplesMath.Citizens).ToList())
            {
                if (!PeoplesFMath.RadioDay(day, f.loadID)) continue;
                Broadcast(f, home);
            }
        }

        /// <summary>The broadcast, if it has something to say: the letter, and the feeling it leaves. True: something was said.</summary>
        public static bool Broadcast(Faction f, Map home, string force = null)
        {
            if (f == null || home == null) return false;
            string what = force ?? PeoplesFMath.Broadcast(f.HasGoodwill ? f.PlayerGoodwill : 0, Papers.Holds(f), f.HostileTo(Faction.OfPlayer));
            if (what == null) return false;
            bool praise = what == "praise";
            string colony = Find.FactionManager.OfPlayer?.Name ?? "?";
            var console = home.listerBuildings.allBuildingsColonist.FirstOrDefault(b => b.def == ThingDefOf.CommsConsole) ?? RelayMast.Of(home);
            Tell.Letter(NewsKind.World, Importance.Notable, home, (praise ? "CC_PeoplesF_RadioPraiseLabel" : "CC_PeoplesF_RadioDenounceLabel").Translate(f.Name).Resolve(),
                (praise ? "CC_PeoplesF_RadioPraise" : "CC_PeoplesF_RadioDenounce").Translate(f.Name.Named("FACTION"), colony.Named("COLONY")).Resolve(),
                praise ? LetterDefOf.PositiveEvent : LetterDefOf.NegativeEvent, console != null ? new LookTargets(console) : LookTargets.Invalid, f);
            var thought = DefDatabase<ThoughtDef>.GetNamedSilentFail(praise ? "Rimfolk_Thought_RadioPraised" : "Rimfolk_Thought_RadioDenounced");
            if (thought != null)
                foreach (var p in home.mapPawns.FreeColonistsSpawned.Where(p => !p.DevelopmentalStage.Baby()))
                    CouncilGameComponent.GiveThought(p, thought);
            return true;
        }
    }

    // ===================== The Hollow Fleet: the shuttle =====================

    /// <summary>
    /// An industrial or spacer colony: the Hollow Fleet's quartermaster calls the colony's radio asking leave to bring the
    /// ship's shuttle down by the gate with goods from the hull. Bidden, it lands (Royalty's shuttle; without it the party
    /// comes down by drop pod), the quartermaster and one to three of the ship's guard step down and trade at the gate as a
    /// visiting trader does, and walk off the map when they are done. The colony may also buy a cryptosleep casket from
    /// the hull ({COUNT} silver, fetched by their runner), which comes down crated in the shuttle's hold.
    /// </summary>
    public class HappeningWorker_FleetShuttle : IncidentWorker_Happening
    {
        private static bool Theirs(Faction f) => Peoples.CallingOf(f) == PeoplesMath.Crew && Arrivals.Usable(f) && EnclaveLanding.CanTrade(f);

        public static ThingDef CasketDef => DefDatabase<ThingDef>.GetNamedSilentFail("CryptosleepCasket");

        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            PeoplesFMath.ShuttleAge((int)Ages.Ours) && map.IsPlayerHome && !MapFacts.InVacuum(map) && map.dangerWatcher.DangerRating == StoryDanger.None
            && Arrivals.Friendly(f => Theirs(f) && !NeutralGroupIncidentUtility.AnyBlockingHostileLord(map, f)) != null && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            ctx.faction = Arrivals.Friendly(f => Theirs(f) && !NeutralGroupIncidentUtility.AnyBlockingHostileLord(map, f));
            if (ctx.faction == null) return false;
            ctx.a = Colonists(map).Where(p => !p.DevelopmentalStage.Juvenile() && !p.Downed).OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0).FirstOrDefault();
            ctx.variant = PeoplesFMath.ShuttleGuards(LivingEvents.WealthFactor(map));
            ctx.thing = CasketDef;
            ctx.count = ctx.thing == null ? 0 : PeoplesFMath.CasketPrice(ctx.thing.BaseMarketValue);
            ctx.placeName = LivingEvents.TownOf(ctx.faction, map) ?? ctx.faction.Name;
            return ctx.a != null;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            if (!ModsConfig.RoyaltyActive) tags.Add("pods");
            if (ctx.thing == null) tags.Add("nocasket");
        }

        public override string Unavailable(HappeningContext ctx, int option) => option == 1 && ctx.thing == null ? "CC_PeoplesF_NoCasket".Translate().Resolve() : null;
    }

    /// <summary>The Fleet's trade party comes down by the gate (FleetLanding.Land), with <see cref="casket"/> a crated casket in the hold.</summary>
    public class DilemmaEffect_FleetShuttle : DilemmaEffect
    {
        public bool casket;

        public override bool ChanceEach => false;

        public override string Unavailable(DilemmaContext ctx)
        {
            var f = ctx?.faction;
            return f == null || f.defeated || f.HostileTo(Faction.OfPlayer) ? "CC_PeoplesF_ShuttleGone".Translate().Resolve() : null;
        }

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (ctx?.faction == null || hc == null) return;
            sheet.Play((ModsConfig.RoyaltyActive ? "CC_PeoplesF_Impact_Shuttle" : "CC_PeoplesF_Impact_ShuttlePods").Translate(ctx.faction.Name, Mathf.Max(1, hc.variant)).Resolve(),
                "CC_PeoplesF_Impact_ShuttleBrief".Translate().Resolve());
            if (casket && hc.thing != null) sheet.Goods(1, hc.thing, 1);
        }

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            if (ctx?.map == null || ctx.faction == null || hc == null) return;
            try
            {
                var extra = new List<Thing>();
                if (casket && hc.thing != null)
                {
                    var c = ThingMaker.MakeThing(hc.thing, hc.thing.MadeFromStuff ? GenStuff.DefaultStuffFor(hc.thing) : null);
                    extra.Add(c.def.Minifiable ? c.MakeMinified() : c);
                }
                FleetLanding.Land(ctx.map, ctx.faction, Mathf.Max(1, hc.variant), extra);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples F: the Fleet's shuttle", e);
            }
        }
    }

    /// <summary>The Fleet's trade party by shuttle (or by drop pod without Royalty), as the enclave's lander comes (EnclaveLanding).</summary>
    public static class FleetLanding
    {
        private static PawnGroupMaker Group(Faction f) => f?.def?.pawnGroupMakers?.FirstOrDefault(g => g.kindDef == PawnGroupKindDefOf.Trader);

        public static bool Land(Map map, Faction f, int guards, List<Thing> extra)
        {
            if (map == null || f == null || f.HostileTo(Faction.OfPlayer) || !EnclaveLanding.CanTrade(f)) return false;
            var group = Group(f);
            var kind = f.def.caravanTraderKinds.RandomElementByWeightWithFallback(t => t.CalculatedCommonality);
            var traderKind = group.traders.Where(o => o?.kind != null && o.kind.trader).RandomElementByWeightWithFallback(o => o.selectionWeight)?.kind;
            if (kind == null || traderKind == null) return false;
            var pawns = new List<Pawn>();
            var trader = PawnGenerator.GeneratePawn(new PawnGenerationRequest(traderKind, f, PawnGenerationContext.NonPlayer, map.Tile));
            trader.mindState.wantsToTradeWithColony = true;
            PawnComponentsUtility.AddAndRemoveDynamicComponents(trader, true);
            if (trader.trader == null) return false;
            trader.trader.traderKind = kind;
            pawns.Add(trader);
            var stock = ThingSetMakerDefOf.TraderStock.root.Generate(new ThingSetMakerParams { traderDef = kind, tile = map.Tile, makingFaction = f });
            foreach (var t in stock)
            {
                if (t is Pawn beast)
                {
                    if (beast.Faction != f) beast.SetFaction(f);
                    pawns.Add(beast);
                }
                else if (!trader.inventory.innerContainer.TryAdd(t) && !t.Destroyed) t.Destroy();
            }
            for (int i = 0; i < guards && !group.guards.NullOrEmpty(); i++)
            {
                var g = group.guards.RandomElementByWeightWithFallback(o => o.selectionWeight)?.kind;
                if (g == null) break;
                pawns.Add(PawnGenerator.GeneratePawn(new PawnGenerationRequest(g, f, PawnGenerationContext.NonPlayer, map.Tile, mustBeCapableOfViolence: true)));
            }
            foreach (var p in pawns)
                if (p.needs?.food != null)
                    p.needs.food.CurLevel = p.needs.food.MaxLevel;
            var things = pawns.Cast<Thing>().Concat(extra ?? new List<Thing>()).ToList();
            IntVec3 spot;
            bool shuttle = ModsConfig.RoyaltyActive && TransportShipDefOf.Ship_Shuttle != null && ShipJobDefOf.Unload != null && ShipJobDefOf.FlyAway != null;
            if (shuttle)
            {
                spot = DropCellFinder.GetBestShuttleLandingSpot(map, f);
                var ship = TransportShipMaker.MakeTransportShip(TransportShipDefOf.Ship_Shuttle, things);
                ship.ArriveAt(spot, map.Parent);
                ship.AddJobs(ShipJobDefOf.Unload, ShipJobDefOf.FlyAway);
            }
            else
            {
                spot = DropCellFinder.TradeDropSpot(map);
                DropPodUtility.DropThingsNear(spot, map, things, 110, false, false, false, false);
            }
            if (!RCellFinder.TryFindRandomSpotJustOutsideColony(spot, map, out var chill)) chill = spot;
            LordMaker.MakeNewLord(f, new LordJob_VisitOnErrand(f, chill, Mathf.RoundToInt(EnclaveLanding.StayHours.RandomInRange * GenDate.TicksPerHour)), map, pawns);
            Tell.Message(NewsKind.World, Importance.Notable, map, (shuttle ? "CC_PeoplesF_ShuttleDown" : "CC_PeoplesF_PodsDown").Translate(f.Name).Resolve(), new LookTargets(spot, map),
                MessageTypeDefOf.NeutralEvent);
            return true;
        }
    }

    // ===================== The Hollow Fleet: salvage =====================

    /// <summary>
    /// The colony has salvage the ship needs (the ship's chunks fallen on its ground, persona cores and advanced components
    /// in its stores: PlayCarry.Salvage), and the Fleet's quartermaster comes for it with two deckhands: the cores and parts
    /// carried out to them, the chunks cut apart where they lie (a bar and sparks at each) and carried off, all for {COUNT}
    /// silver, paid at the gate. The colony's crafter may watch them take a chunk apart. {TALE} is what there is.
    /// </summary>
    public class HappeningWorker_FleetSalvage : HappeningWorker_Visitors
    {
        /// <summary>The ship's chunks on the colony's ground: out of the fog, reachable from the gate.</summary>
        public static List<Thing> Chunks(Map map)
        {
            if (map == null) return new List<Thing>();
            var gate = PeoplesD.GateSpot(map);
            return map.listerThings.ThingsOfDef(ThingDefOf.ShipChunk).Where(t => t.Spawned && !t.Position.Fogged(map)
                && (gate == null || map.reachability.CanReach(gate.Value, t, PathEndMode.Touch, TraverseParms.For(TraverseMode.PassDoors)))).Take(PeoplesFMath.CutMax).ToList();
        }

        public static float Worth(Map map, out int items, out int chunks)
        {
            var stock = PlayCarry.Stock(map, PlayCarry.Salvage, null).ToList();
            items = stock.Sum(t => t.stackCount);
            chunks = Chunks(map).Count;
            return stock.Sum(t => t.MarketValue * t.stackCount) + chunks * PeoplesFMath.ChunkWorth;
        }

        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            PeoplesD.Friendly(PeoplesMath.Crew) != null && Worth(map, out _, out _) >= PeoplesFMath.SalvageMinWorth && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Crew);
            if (ctx.faction == null) return false;
            float worth = Worth(ctx.map, out int items, out int chunks);
            if (worth < PeoplesFMath.SalvageMinWorth) return false;
            ctx.count = PeoplesFMath.SalvagePrice(worth);
            ctx.variant = items;
            ctx.stranger = Arrivals.Make(PeoplesD.TraderKind(ctx.faction), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            for (int i = 0; i < 2; i++) ctx.group.Add(Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_HollowFleet_Deckhand"), ctx.faction, ext.ages));
            var parts = new List<string>();
            if (chunks > 0) parts.Add("CC_PeoplesF_SalvageChunks".Translate(chunks).Resolve());
            if (items > 0) parts.Add(PlayCarry.Label(PlayCarry.Salvage, null) + " (" + items + ")");
            ctx.tale = TextUtil.JoinAnd(parts);
            ctx.note = chunks > 0 ? "chunks" : null;
            ctx.a = PeoplesD.Best(ctx.map, SkillDefOf.Crafting) ?? PeoplesD.Able(ctx.map).RandomElementWithFallback();
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(ctx.group.Prepend(ctx.stranger).ToList(), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            if (ctx.note == "chunks") tags.Add("chunks");
            if (ctx.variant > 0) tags.Add("cores");
        }

        public override string Unavailable(HappeningContext ctx, int option)
        {
            if (option <= 1 && Worth(ctx.map, out _, out _) < PeoplesFMath.SalvageMinWorth) return "CC_PeoplesF_SalvageGone".Translate().Resolve();
            return option == 1 && Chunks(ctx.map).Count == 0 ? "CC_PeoplesF_NoChunks".Translate().Resolve() : null;
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (option <= 1 && !byDefault) Couriers.PaySilver(ctx.map, VarietyUtil.Near(ctx.a, ctx.map), ctx.count, Couriers.PeopleOf(ctx), ctx.stranger);
            if (!byDefault) PeoplesF.Say(ctx.stranger, Ext, ctx, option <= 1 ? "salvage" : "thanks");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The salvage sold: the cores and parts carried out to the quartermaster, the chunks cut apart where they lie by the deckhands (FleetSalvage.Cut).</summary>
    public class DilemmaEffect_FleetSalvage : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var map = ctx?.map;
            if (map == null) return;
            HappeningWorker_FleetSalvage.Worth(map, out int items, out int chunks);
            if (items > 0) sheet.Goods(-1, PlayCarry.Label(PlayCarry.Salvage, null), items, PlayCarry.Salvage);
            if (chunks > 0) sheet.Play("CC_PeoplesF_Impact_Cut".Translate(chunks).Resolve(), "CC_PeoplesF_Impact_CutBrief".Translate().Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            var map = ctx?.map;
            if (hc == null || map == null) return;
            var stock = PlayCarry.Stock(map, PlayCarry.Salvage, null).ToList();
            int items = stock.Sum(t => t.stackCount);
            if (items > 0) Couriers.TakeFor(ctx, PlayCarry.Salvage, null, items);
            var chunks = HappeningWorker_FleetSalvage.Chunks(map);
            if (chunks.Count == 0) return;
            var hands = hc.group.Where(p => PeoplesF.Up(p, map)).ToList();
            if (PlayOuts.On && hands.Count > 0)
            {
                PeoplesD.Gather(hands, ctx.faction, chunks[0].Position, PeoplesFMath.CutHours * chunks.Count + 2);
                // The deckhands share the chunks out between them, a queue each.
                for (int i = 0; i < hands.Count; i++)
                {
                    var mine = chunks.Where((c, j) => j % hands.Count == i).Select(c => (LocalTargetInfo)c).ToList();
                    if (mine.Count == 0) continue;
                    if (!PeoplesE.Start(hands[i], PeoplesF.CutJob, mine[0], mine.Skip(1).ToList(), PeoplesFMath.CutHours * GenDate.TicksPerHour))
                        foreach (var c in mine) FleetSalvage.Cut(hands[i], c.Thing, false);
                }
                return;
            }
            foreach (var c in chunks) FleetSalvage.Cut(ctx.stranger, c, false);
        }
    }

    /// <summary>A ship's chunk cut apart and carried off by the Fleet's deckhands.</summary>
    public static class FleetSalvage
    {
        public static void Cut(Pawn by, Thing chunk, bool last)
        {
            if (chunk == null || chunk.Destroyed) return;
            var map = chunk.MapHeld;
            var at = chunk.Position;
            if (chunk.Spawned) chunk.Destroy(DestroyMode.Vanish);
            if (map != null) FleckMaker.ThrowMicroSparks(at.ToVector3Shifted(), map);
            if (!last) return;
            Tell.Message(NewsKind.Land, Importance.Notable, map, "CC_PeoplesF_ChunksCut".Translate((by?.Faction?.Name ?? "?").Named("FACTION")).Resolve(), by != null && by.Spawned ? new LookTargets(by) : LookTargets.Invalid,
                MessageTypeDefOf.NeutralEvent);
            if (by != null && by.Spawned && by.Faction != null && !by.Faction.IsPlayer) Arrivals.SendAway(new List<Pawn> { by });
        }
    }

    // ===================== The Hollow Fleet: the sleeper woken too late =====================

    /// <summary>
    /// Towards evening a sleeper from the Hollow Fleet's caskets comes to the gate, in a ship's uniform two hundred years
    /// out of date and still sick from the long sleep: woken too late, everyone they knew aboard long dead, they ask the
    /// colony for shelter. Taken in, they are the colony's; a night in a warm corner and the road at first light; or the
    /// gate stays shut. {COUNT} is the years they slept.
    /// </summary>
    public class HappeningWorker_FleetSleeper : HappeningWorker_Visitors
    {
        /// <summary>The Fleet the sleeper woke in: any people of its calling still standing, at peace or not.</summary>
        public static Faction Fleet() =>
            Find.FactionManager.AllFactionsListForReading.Where(f => Peoples.CallingOf(f) == PeoplesMath.Crew && !f.defeated && !f.IsPlayer && f.def.humanlikeFaction)
                .RandomElementWithFallback();

        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            Fleet() != null && PeoplesFMath.SleeperHour(GenLocalDate.HourOfDay(map)) && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = Fleet();
            if (ctx.faction == null) return false;
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_HollowFleet_Deckhand"), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            ctx.count = PeoplesFMath.SleptYears(Rand.Value);
            try
            {
                ctx.stranger.ageTracker.AgeChronologicalTicks += (long)ctx.count * GenDate.TicksPerYear;
                var sick = HediffDefOf.CryptosleepSickness;
                if (sick != null) ctx.stranger.health.AddHediff(sick);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples F: the sleeper's long sleep", e);
            }
            ctx.xWaiting = false;
            ctx.a = PeoplesD.Best(ctx.map, SkillDefOf.Social) ?? PeoplesD.Able(ctx.map).RandomElementWithFallback();
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(new List<Pawn> { ctx.stranger }, ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), PeoplesMath.HoursToLight(GenLocalDate.HourOfDay(ctx.map), 6)) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            if (ctx.faction != null && ctx.faction.HostileTo(Faction.OfPlayer)) tags.Add("theirfoe");
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesF.Say(ctx.stranger, Ext, ctx, option == 0 ? "taken" : option == 1 ? "night" : "refused");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    // ===================== Dev mode =====================

    /// <summary>Dev mode: release F's state at a glance, and its slow things brought on now (Debug actions, "Rimfolk", "Peoples: ...").</summary>
    public static class PeoplesFDebug
    {
        private static void Say(string text) => Messages.Message("Peoples: " + text, MessageTypeDefOf.SilentInput, false);

        [DebugAction("Rimfolk", "Peoples: release F's strikes, papers, conscript and masts (log)", allowedGameStates = AllowedGameStates.Playing)]
        private static void Log()
        {
            var sb = new System.Text.StringBuilder("[Rimfolk] Peoples, release F:");
            foreach (var f in Find.FactionManager.AllFactionsListForReading.Where(f => Peoples.CallingOf(f) == PeoplesMath.Drillers))
                sb.Append("\n  " + f.Name + ": " + (UnionStrike.Striking(f) ? "on strike" : "working"));
            foreach (var f in Find.FactionManager.AllFactionsListForReading.Where(f => Peoples.CallingOf(f) == PeoplesMath.Citizens))
                sb.Append("\n  " + f.Name + ": papers " + (Papers.Holds(f) ? "held" : "none") + ", terms " + Papers.Terms(f).ToStringPercent()
                          + ", radio would say " + (PeoplesFMath.Broadcast(f.HasGoodwill ? f.PlayerGoodwill : 0, Papers.Holds(f), f.HostileTo(Faction.OfPlayer)) ?? "nothing"));
            var c = Conscripts.Serving();
            sb.Append("\n  conscript: " + (c == null ? "none" : (c.x?.LabelShort ?? "?") + ", stage " + c.stake));
            foreach (var m in Find.Maps.Where(m => m.IsPlayerHome))
            {
                var mast = RelayMast.Of(m);
                sb.Append("\n  " + m + ": mast " + (mast == null ? "none" : mast.HitPoints + "/" + mast.MaxHitPoints + (RelayMast.Working(m) ? ", singing" : ", quiet"))
                          + ", radio " + RepublicRadio.HasRadio(m));
            }
            Verse.Log.Message(sb.ToString());
            Say("logged.");
        }

        [DebugAction("Rimfolk", "Peoples: the Deepwell Union strikes now (testing)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Strike()
        {
            var f = Find.FactionManager.AllFactionsListForReading.FirstOrDefault(x => Peoples.CallingOf(x) == PeoplesMath.Drillers && !x.defeated && !UnionStrike.Striking(x));
            if (f == null)
            {
                Say("no Deepwell Union to strike.");
                return;
            }
            UnionStrike.Call(f, Find.CurrentMap, PeoplesF.Now);
        }

        [DebugAction("Rimfolk", "Peoples: the Republic's radio now: what it would say, praise or denounce (testing)...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Radio()
        {
            var f = Find.FactionManager.AllFactionsListForReading.FirstOrDefault(x => Peoples.CallingOf(x) == PeoplesMath.Citizens && !x.defeated);
            if (f == null)
            {
                Say("no Last Republic in this world.");
                return;
            }
            var opts = new List<FloatMenuOption>();
            foreach (var what in new[] { null, "praise", "denounce" })
            {
                string w = what;
                opts.Add(new FloatMenuOption(w ?? "what it would say", () =>
                {
                    if (!RepublicRadio.Broadcast(f, Find.CurrentMap, w)) Say("the radio has nothing to say of the colony this season.");
                }));
            }
            Find.WindowStack.Add(new FloatMenu(opts));
        }

        [DebugAction("Rimfolk", "Peoples: the conscript's season is out now (testing)", allowedGameStates = AllowedGameStates.Playing)]
        private static void ConscriptHome()
        {
            if (Conscripts.Serving() == null) Say("no conscript serving.");
            else Conscripts.Recall();
        }

        [DebugAction("Rimfolk", "Peoples: the colony's mast in a storm, to half its strength (testing)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void MastWorn()
        {
            var mast = RelayMast.Of(Find.CurrentMap);
            if (mast == null)
            {
                Say("the colony has no relay mast here.");
                return;
            }
            mast.HitPoints = Mathf.Max(1, Mathf.RoundToInt(mast.MaxHitPoints * PeoplesFMath.QuietBelow) - 1);
            Say("the mast is quiet now; mend it, or wait for a lineman in a storm.");
        }
    }
}
