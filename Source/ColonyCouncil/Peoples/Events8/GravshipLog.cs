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
    // ===================== 75. The colony's gravship (the Hollow Fleet) =====================

    /// <summary>
    /// The colony's gravship (item 75): with Odyssey, when the colony has a gravship (its grav engine at home), the Hollow Fleet calls it
    /// on the ship's own channel: its officers would come down and walk its decks with the colony's pilot, and log it as the Fleet logs
    /// every ship. Bidden, two officers come down by shuttle near the ship (Royalty's; without it by drop pod; a Fleet before the
    /// industrial age walks in), walk from part to part (the engine, the pilot console, the thrusters and tanks: Odyssey's own list of
    /// the ship's parts) with the pilot at the engine, and log it. Then the part they brought from the hull's stores is set down at the
    /// engine (a ship's upgrade of Odyssey's or Vanilla Gravship Expanded's, by its trade tag; gravlite panels when none will do), or
    /// with the second answer the pilot learns the ship's systems from them. The pilot has a good memory of it, the Fleet thinks better
    /// of the colony (+10), and the officers walk off. One of the events' visits (kind "pev:gravlog"): x and group the officers, a the
    /// pilot, place the grav engine, stake 1 for the lesson, due 1 once they walk the decks, its text "people|part|count".
    /// </summary>
    public static class GravshipLog
    {
        public const string Kind = "pev:gravlog";

        /// <summary>The visit's due when the officers could get aboard nowhere and logged the ship from outside (0: not down yet; 1: the walk set going).</summary>
        private const int Outside = 2;
        public const string EventDef = "Rimfolk_Evt_GravshipLog";

        /// <summary>Odyssey's ship upgrades carry this trade tag, Vanilla Gravship Expanded's too.</summary>
        public const string UpgradeTag = "GravshipUpgrade";

        private static int Now => Find.TickManager.TicksGame;

        /// <summary>The colony's grav engine at this home (its gravship), when Odyssey is on.</summary>
        public static Building Engine(Map map)
        {
            if (!ModsConfig.OdysseyActive || map == null || ThingDefOf.GravEngine == null) return null;
            return map.listerBuildings.AllBuildingsColonistOfDef(ThingDefOf.GravEngine).FirstOrDefault(b => b != null && b.Spawned && !b.Position.Fogged(map));
        }

        /// <summary>The ship's name, as its engine carries it.</summary>
        public static string ShipName(Building engine) => (engine as Building_GravEngine)?.RenamableLabel ?? engine?.LabelCap ?? "?";

        /// <summary>The parts the officers walk to: the engine, then the ship's own parts nearest it (Odyssey's list), five at most.</summary>
        public static List<Thing> Parts(Building engine)
        {
            var list = new List<Thing>();
            if (engine == null) return list;
            list.Add(engine);
            try
            {
                if (engine is Building_GravEngine g)
                    list.AddRange(g.GravshipComponents.Select(c => c?.parent).Where(t => t != null && t.Spawned && t != engine).Distinct()
                        .OrderBy(t => t.Position.DistanceToSquared(engine.Position)).Take(PeopleEvents8Math.DeckParts - 1));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 8: the ship's parts", e);
            }
            return list;
        }

        /// <summary>
        /// A part from the hull's stores: a ship's upgrade of the player's mods (the trade tag), one that can be carried (an item, or a
        /// building packed up), not one of the colony's already, else gravlite panels.
        /// </summary>
        public static ThingDef Part(Map map, out int count)
        {
            count = 1;
            var have = map == null ? new HashSet<ThingDef>() : new HashSet<ThingDef>(map.listerBuildings.allBuildingsColonist.Select(b => b.def));
            var parts = DefDatabase<ThingDef>.AllDefsListForReading.Where(d => d.tradeTags != null && d.tradeTags.Contains(UpgradeTag) && !have.Contains(d)
                                                                             && (d.category == ThingCategory.Item || d.category == ThingCategory.Building && d.Minifiable) && d.BaseMarketValue < 3000f).ToList();
            var pick = parts.Where(d => d.category == ThingCategory.Building).RandomElementWithFallback();
            if (pick != null) return pick;
            count = PeopleEvents8Math.PanelCount;
            return ThingDefOf.GravlitePanel ?? parts.FirstOrDefault();
        }

        /// <summary>The colony's pilot: whoever wears the pilot's implant, else its best with the ship's systems (Intellectual).</summary>
        public static Pawn Pilot(Map map)
        {
            var implant = DefDatabase<HediffDef>.GetNamedSilentFail("PilotAssistant");
            var able = PeoplesD.Able(map).Where(p => !p.DevelopmentalStage.Juvenile()).ToList();
            return able.FirstOrDefault(p => implant != null && p.health.hediffSet.HasHediff(implant)) ?? PeoplesD.Best(map, SkillDefOf.Intellectual, p => !p.DevelopmentalStage.Juvenile());
        }

        /// <summary>
        /// They come down near the ship: by shuttle (Royalty's), by drop pod without it, on foot for a Fleet before the industrial age; a
        /// visit at the engine for the day. True when they are on the way.
        /// </summary>
        public static bool Land(Map map, Faction f, List<Pawn> pawns, IntVec3 near)
        {
            if (map == null || f == null || pawns.NullOrEmpty()) return false;
            foreach (var p in pawns)
                if (p.needs?.food != null)
                    p.needs.food.CurLevel = p.needs.food.MaxLevel;
            bool flies = PeopleEvents.AgeOf(f) >= 2;
            if (!flies) return Arrivals.Visit(pawns, map, f, near, 12f) != null;
            bool shuttle = ModsConfig.RoyaltyActive && TransportShipDefOf.Ship_Shuttle != null && ShipJobDefOf.Unload != null && ShipJobDefOf.FlyAway != null;
            IntVec3 spot;
            if (shuttle)
            {
                spot = DropCellFinder.GetBestShuttleLandingSpot(map, f);
                var ship = TransportShipMaker.MakeTransportShip(TransportShipDefOf.Ship_Shuttle, pawns.Cast<Thing>().ToList());
                ship.ArriveAt(spot, map.Parent);
                ship.AddJobs(ShipJobDefOf.Unload, ShipJobDefOf.FlyAway);
            }
            else
            {
                if (!DropCellFinder.TryFindDropSpotNear(near, map, out spot, false, false, false)) spot = DropCellFinder.TradeDropSpot(map);
                DropPodUtility.DropThingsNear(spot, map, pawns.Cast<Thing>(), 110, false, false, false, false);
            }
            LordMaker.MakeNewLord(f, new LordJob_VisitOnErrand(f, near, 12 * GenDate.TicksPerHour), map, pawns);
            Tell.Message(NewsKind.World, Importance.Notable, map, (shuttle ? "CC_PEv8_GravShuttleDown" : "CC_PEv8_GravPodsDown").Translate(f.Name).Resolve(), new LookTargets(spot, map),
                MessageTypeDefOf.NeutralEvent);
            return true;
        }

        /// <summary>The answer taken: two officers made and sent down, the part from the hull's stores in the first one's pack.</summary>
        public static void Begin(HappeningContext ctx, bool lesson)
        {
            var map = ctx?.map;
            var t = CouncilGameComponent.Instance?.happenings;
            var f = ctx?.faction;
            var engine = ctx?.place as Building;
            if (map == null || t == null || f == null || engine == null || engine.Destroyed) return;
            var officers = new List<Pawn>();
            for (int i = 0; i < PeopleEvents8Math.Officers; i++)
            {
                var p = Arrivals.Make(PeoplesD.KindNamed(f, "Rimfolk_HollowFleet_Officer"), f, new FloatRange(28f, 60f));
                if (p != null) officers.Add(p);
            }
            if (officers.Count == 0) return;
            var x = officers[0];
            if (!lesson && ctx.thing != null)
            {
                try
                {
                    var part = ThingMaker.MakeThing(ctx.thing, ctx.thing.MadeFromStuff ? GenStuff.DefaultStuffFor(ctx.thing) : null);
                    part.stackCount = Mathf.Max(1, Mathf.Min(ctx.count, ctx.thing.stackLimit));
                    var carried = ctx.thing.category == ThingCategory.Building ? part.MakeMinified() : part;
                    if (!x.inventory.innerContainer.TryAdd(carried) && !carried.Destroyed) carried.Destroy();
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("peoples' events 8: the part from the hull", e);
                }
            }
            if (!Land(map, f, officers, engine.Position))
            {
                foreach (var p in officers) Arrivals.Discard(p);
                return;
            }
            var v = t.AddVisit(Kind, ctx.incident, map, x, ctx.a, officers.Skip(1), Now + 16 * GenDate.TicksPerHour);
            v.place = engine;
            v.stake = lesson ? 1 : 0;
            v.text = f.GetUniqueLoadID() + "|" + (lesson ? "" : ctx.thing?.defName ?? "") + "|" + ctx.count;
        }

        /// <summary>The visit's hour: once they are down, the walk of the decks; once it is done (or the day runs out), the log.</summary>
        public static bool Hour(Visit v, int now)
        {
            if (v.done) return false;
            var x = v.x;
            var map = v.map;
            var engine = v.place;
            if (x == null || x.Dead || engine == null || engine.Destroyed)
            {
                Logged(v);
                return false;
            }
            if (!x.Spawned)
            {
                if (now > v.until) v.done = true;
                return !v.done;
            }
            if (v.due == 0)
            {
                if (!PlayOuts.On || x.Map != map)
                {
                    Logged(v);
                    return false;
                }
                // The colony asked them aboard: the ship's rooms with beds in them are open to them for the walk (KeepOut, PeopleErrands),
                // counted from now. A part still out of reach (a cold store, someone asleep in the room) is passed over.
                KeepOut.Refresh(map);
                var parts = Parts(engine as Building).Where(t => x.CanReach(t, PathEndMode.Touch, Danger.Some)).Select(t => (LocalTargetInfo)t).ToList();
                v.due = 1;
                if (parts.Count == 0 || !PeopleEvents8.Send(x, PeopleEvents8.DeckJob, parts[0], PeopleEvents8Math.DeckTicks, parts.Skip(1).ToList()))
                {
                    // Nowhere aboard they could get to: they log the ship from the outside, and the words say so.
                    v.due = Outside;
                    Logged(v);
                    return false;
                }
                foreach (var o in v.group.Where(o => PeoplesG.Up(o, map)))
                    PeopleEvents8.Send(o, PeopleEvents8.DeckJob, parts.Last(), PeopleEvents8Math.DeckTicks, parts.Take(parts.Count - 1).Reverse().ToList());
                var pilot = v.a;
                if (PeoplesG.Up(pilot, map) && !pilot.Drafted && !pilot.InMentalState)
                {
                    var cell = CellFinder.StandableCellNear(engine.Position, map, 3f);
                    if (cell.IsValid) PeopleEvents8.Send(pilot, PeopleEvents8.StandByJob, cell, PeopleEvents8Math.DeckTicks * parts.Count + GenDate.TicksPerHour);
                }
                PeoplesG.SayLine(x, v.incident, "aboard");
                return true;
            }
            if (x.CurJobDef == PeopleEvents8.DeckJob && now <= v.until) return true;
            Logged(v);
            return false;
        }

        /// <summary>A part walked and looked over: now and then a word about it.</summary>
        public static void Walked(Pawn officer, Thing part)
        {
            if (officer == null || part == null || !Rand.Chance(0.4f)) return;
            var v = PeopleEvents8.Of(Kind, officer) ?? CouncilGameComponent.Instance?.happenings?.visits?.FirstOrDefault(x => x != null && x.kind == Kind && !x.done && x.group.Contains(officer));
            if (v != null) PeoplesG.SayLine(officer, v.incident, "deck");
        }

        /// <summary>The ship logged, once: the part set down at the engine or the lesson, the pilot's memory, the Fleet's thanks, told; the officers go.</summary>
        private static void Logged(Visit v)
        {
            if (v.done) return;
            v.done = true;
            var x = v.x;
            var map = v.map;
            var engine = v.place;
            var pilot = v.a;
            var f = x?.Faction ?? PeopleEvents8.PeopleOf(v);
            bool outside = v.due == Outside;
            bool lesson = v.stake == 1 && !outside;
            string ship = ShipName(engine as Building);
            string part = null;
            if (v.stake != 1 && x != null)
            {
                var held = x.inventory?.innerContainer?.FirstOrDefault(t => t != null && !(t is Pawn) && t.def != ThingDefOf.Silver && (t.def.tradeTags?.Contains(UpgradeTag) == true
                                                                                                                                     || t is MinifiedThing m && m.InnerThing?.def.tradeTags?.Contains(UpgradeTag) == true));
                if (held != null)
                {
                    var at = engine != null && engine.Spawned && !outside ? engine.Position : x.Spawned ? x.Position : engine != null && engine.Spawned ? engine.Position : IntVec3.Invalid;
                    if (at.IsValid && map != null && x.inventory.innerContainer.TryDrop(held, at, map, ThingPlaceMode.Near, out var dropped))
                    {
                        dropped.SetForbidden(false, false);
                        part = dropped.LabelCap;
                    }
                }
            }
            if (lesson && pilot != null && !pilot.Dead) pilot.skills?.Learn(SkillDefOf.Intellectual, PeopleEvents8Math.PilotXp, true);
            if (pilot != null && !pilot.Dead && !outside) PeopleEvents8.Thought(pilot, "Rimfolk_Thought_ShipLogged");
            if (x != null && x.Spawned) PeoplesG.SayLine(x, v.incident, "logged");
            string key = outside ? (part != null ? "CC_PEv8_GravOutsidePart" : "CC_PEv8_GravOutside")
                : part != null ? "CC_PEv8_GravLoggedPart" : lesson ? "CC_PEv8_GravLoggedLesson" : "CC_PEv8_GravLogged";
            Tell.Message(NewsKind.World, Importance.Notable, map, key.Translate((f?.Name ?? "?").Named("FACTION"), ship.Named("SHIP"), (pilot?.LabelShort ?? "?").Named("PILOT"),
                (part ?? "").Named("PART")).Resolve(), engine != null && engine.Spawned ? new LookTargets(engine) : LookTargets.Invalid, MessageTypeDefOf.PositiveEvent);
            PeopleEvents8.Goodwill(f, PeopleEvents8Math.GravGoodwill, GoodwillWhy.Welcome);
            Arrivals.SendAway(v.group.Prepend(x).Where(p => p != null).ToList());
        }

        /// <summary>Dev mode: the Fleet's call about the colony's gravship now.</summary>
        public static string Now_(Map map)
        {
            var f = PeoplesD.Friendly(PeoplesMath.Crew);
            if (map == null || f == null) return "no Hollow Fleet at peace.";
            if (Engine(map) == null) return "no gravship here (Odyssey's grav engine, the colony's).";
            return PeopleEvents2.Fire(EventDef, map, f) ? "the Fleet calls the colony's gravship." : "the call could not come now (a question waits, or a threat is on).";
        }
    }

    /// <summary>
    /// The Hollow Fleet ({FACTION}) calls the colony's gravship ({TALE}) on the ship's channel: its officers would come down, walk the
    /// decks with the pilot ({A}) and log it, and bring a part from the hull's stores ({THING}, {COUNT} of it) or teach the pilot the ship.
    /// </summary>
    public class HappeningWorker_GravshipLog : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            PeoplesD.Friendly(PeoplesMath.Crew) != null && GravshipLog.Engine(map) != null && GravshipLog.Pilot(map) != null && !MapFacts.ThreatOn(map);

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Crew);
            var engine = GravshipLog.Engine(ctx.map);
            if (ctx.faction == null || engine == null) return false;
            ctx.place = engine;
            ctx.tale = GravshipLog.ShipName(engine);
            ctx.thing = GravshipLog.Part(ctx.map, out int count);
            ctx.count = count;
            ctx.a = GravshipLog.Pilot(ctx.map);
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return ctx.a != null && ctx.thing != null;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            PeopleEvents.AgeTags(tags, ctx.faction);
            if (ctx.count > 1) tags.Add("panels");
            if (PeopleEvents.AgeOf(ctx.faction) >= 2) tags.Add(ModsConfig.RoyaltyActive ? "shuttle" : "pods");
        }

        public override string Unavailable(HappeningContext ctx, int option)
        {
            if (option > 1) return null;
            if (ctx.place == null || ctx.place.Destroyed || !ctx.place.Spawned) return "CC_PEv8_ShipGone".Translate().Resolve();
            if (ctx.a == null || ctx.a.Dead || !ctx.a.Spawned || ctx.a.Map != ctx.map) return "CC_PEv8_PilotAway".Translate(ctx.a?.LabelShort ?? "?").Resolve();
            return null;
        }
    }

    /// <summary>The officers come down and walk the decks (GravshipLog.Begin); with <see cref="lesson"/> the pilot learns the ship from them instead of the part.</summary>
    public class DilemmaEffect_GravshipLog : DilemmaEffect
    {
        public bool lesson;

        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc?.a == null || hc.place == null) return;
            sheet.Play("CC_PEv8_Impact_Decks".Translate(hc.tale ?? "?", hc.a.LabelShort).Resolve(), "CC_PEv8_Impact_DecksBrief".Translate().Resolve());
            if (lesson)
                sheet.Add(ImpactKind.Skill, 1, "CC_PEv8_Impact_PilotLesson".Translate(hc.a.LabelShort, SkillDefOf.Intellectual.label).Resolve(), "CC_PEv8_Impact_PilotLessonBrief".Translate().Resolve(),
                    "pev8pilot");
            else if (hc.thing != null) sheet.Goods(1, hc.thing, Mathf.Max(1, hc.count));
            sheet.Add(ImpactKind.Mood, 1, "CC_PEv8_Impact_Logged".Translate(hc.a.LabelShort).Resolve(), "CC_PEv8_Impact_LoggedBrief".Translate().Resolve(), "pev8logged");
            if (hc.faction != null) sheet.Goodwill(hc.faction, PeopleEvents8Math.GravGoodwill);
        }

        public override void Apply(DilemmaContext ctx) => GravshipLog.Begin(ctx as HappeningContext, lesson);
    }
}
