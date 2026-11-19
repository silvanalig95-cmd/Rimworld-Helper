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
    // ===================== 28. Lanterns on the water (the river folk) =====================

    /// <summary>
    /// Lanterns on the water (item 28): on their knot night, boatmen of the river folk set lanterns at dusk on the colony's
    /// water for everyone it lost or took in, and the colony may gather on the bank with them; those who come keep a memory
    /// of it. With Goji's Fantasy Race: Merren, its floating lanterns are set on the water and stay there, the colony's;
    /// otherwise torches along the bank, which the boatmen take back in the morning. Where a crossing of theirs stands, the
    /// boatmen resting at it on that night set them there instead (round 2's knot night, DilemmaEffect_KnotRest). The
    /// calendar is theirs: the same knot day as the crossing's rest (PeopleEvents2Math.KnotDay).
    /// </summary>
    public static class KnotLanterns
    {
        public const string Kind = "pev:lanterns";
        public const string EventDef = "Rimfolk_Evt_KnotLanterns";

        /// <summary>Goji's Fantasy Race: Merren's floating lantern (a mod's, by name), else the game's torch lamp.</summary>
        public static ThingDef Floating => DefDatabase<ThingDef>.GetNamedSilentFail("Stagz_WaterCandle");

        public static ThingDef Torch => DefDatabase<ThingDef>.GetNamedSilentFail("TorchLamp");

        private static int Now => Find.TickManager.TicksGame;

        /// <summary>Dev mode: it is a river folk's knot night now, whatever the day.</summary>
        public static bool force;

        /// <summary>The colony's dead (colonists in its graves, and those lost elsewhere), the most recent first, by name.</summary>
        public static List<string> Lost(Map map)
        {
            var dead = new List<Pawn>();
            foreach (var g in map?.listerBuildings?.allBuildingsColonist.OfType<Building_Grave>() ?? Enumerable.Empty<Building_Grave>())
                if (g.Corpse?.InnerPawn is Pawn p && p.RaceProps.Humanlike && p.Faction == Faction.OfPlayer) dead.Add(p);
            foreach (var p in Find.WorldPawns?.AllPawnsDead ?? Enumerable.Empty<Pawn>())
                if (p != null && p.RaceProps?.Humanlike == true && p.Faction == Faction.OfPlayer && !dead.Contains(p)) dead.Add(p);
            return dead.OrderByDescending(p => p.Corpse?.timeOfDeath ?? 0).Select(p => p.LabelShort).ToList();
        }

        /// <summary>The colony's people who came to it after the start (it took them in).</summary>
        public static int TakenIn(Map map) =>
            IncidentWorker_Happening.Colonists(map).Count(p => (p.records?.GetAsInt(RecordDefOf.TimeAsColonistOrColonyAnimal) ?? 0) < Find.TickManager.TicksGame - GenDate.TicksPerDay * 2);

        /// <summary>A crossing of theirs stands on this map (their knot night is kept there, round 2's).</summary>
        public static bool Crossing(Map map, Faction f) =>
            PeopleEvents2Book.Instance?.crossings?.Any(r => r != null && r.map == map && r.faction == f && RiverCrossing.Stands(r)) ?? false;

        /// <summary>The river folk whose knot night it is at this map now, at dusk, the lanterns not set this year, no crossing of theirs here.</summary>
        public static Faction Tonight(Map map)
        {
            if (map == null) return null;
            var b = PeopleEvents4Book.Instance;
            int year = GenLocalDate.Year(map);
            bool dusk = PeopleEvents4Math.LanternHour(GenLocalDate.HourOfDay(map));
            int day = GenLocalDate.DayOfYear(map);
            Faction found = null;
            foreach (var f in Find.FactionManager.AllFactionsVisible)
            {
                if (!PeoplesD.Theirs(f, PeoplesMath.Fishers) || f.HostileTo(Faction.OfPlayer)) continue;
                if (b != null && b.LanternsCame(f, year)) continue;
                if (!force && (!dusk || day != PeopleEvents2Math.KnotDay(f.loadID))) continue;
                if (Crossing(map, f)) continue;
                found = f;
                break;
            }
            // The bank last: it is looked for only on the night itself.
            return found != null && HappeningWorker_RiverNets.Bank(map).IsValid ? found : null;
        }

        /// <summary>The homes' hour: on a knot night at dusk, the boatmen's letter (the lanterns come once a year).</summary>
        public static void Watch(Map map, int now)
        {
            var f = Tonight(map);
            if (f == null || !Tell.CanAsk) return;
            if (PeopleEvents2.Fire(EventDef, map, f))
            {
                PeopleEvents4Book.Instance?.NoteLanterns(f, GenLocalDate.Year(map));
                force = false;
            }
        }

        /// <summary>The light they set down on the bank as they come (a torch lamp of theirs), where the colony would gather.</summary>
        public static Thing BankLight(Map map, IntVec3 bank, Faction f)
        {
            var def = Torch;
            if (def == null || map == null || !bank.IsValid) return null;
            try
            {
                var t = ThingMaker.MakeThing(def, def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null);
                t.SetFaction(f);
                if (!GenPlace.TryPlaceThing(t, bank, map, ThingPlaceMode.Near, out var placed)) return null;
                Fuel(placed);
                return placed;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 4: the bank light", e);
                return null;
            }
        }

        private static void Fuel(Thing t)
        {
            var fuel = t?.TryGetComp<CompRefuelable>();
            if (fuel != null && fuel.Fuel < fuel.Props.fuelCapacity) fuel.Refuel(fuel.Props.fuelCapacity - fuel.Fuel);
        }

        /// <summary>
        /// The lanterns set going: the boatmen walk to the water at the bank one after another and set them (so many), then
        /// rest by the bank until the morning; <paramref name="light"/> is their light there.
        /// </summary>
        public static Visit Begin(Map map, Faction f, List<Pawn> boatmen, IntVec3 bank, Thing light, int lanterns, IncidentDef incident)
        {
            var t = CouncilGameComponent.Instance?.happenings;
            var party = boatmen?.Where(p => PeoplesG.Up(p, map)).ToList();
            if (t == null || map == null || f == null || party == null || party.Count == 0 || !bank.IsValid) return null;
            int hour = GenLocalDate.HourOfDay(map);
            int hours = (24 - hour + 7) % 24;
            if (hours < 6) hours += 24;
            try
            {
                Lords.Leave(party);
                LordMaker.MakeNewLord(f, new LordJob_VisitOnErrand(f, bank, hours * GenDate.TicksPerHour), map, party);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 4: the boatmen at the bank", e);
            }
            var v = t.AddVisit(Kind, incident ?? DefDatabase<IncidentDef>.GetNamedSilentFail(EventDef), map, party[0], null, party.Skip(1).ToList(), Now + hours * GenDate.TicksPerHour);
            v.cell = bank;
            v.place = light;
            v.stake = Mathf.Min(PeopleEvents4Math.LanternsSet, lanterns);
            v.text = f.GetUniqueLoadID() + "|";
            return v;
        }

        /// <summary>Water by the bank where a lantern could go: open water near the light, none set there yet.</summary>
        private static IntVec3 NextWater(Visit v, Pawn p)
        {
            var map = v.map;
            var floating = Floating;
            foreach (var c in GenRadial.RadialCellsAround(v.cell, 9f, false))
            {
                if (!c.InBounds(map) || !(c.GetTerrain(map)?.IsWater ?? false) || c.Fogged(map) || c.GetFirstBuilding(map) != null) continue;
                if (floating == null)
                {
                    // Without the floating lanterns: the dry bank cell beside this water, for a torch.
                    var dry = GenAdj.CellsAdjacentCardinal(c, Rot4.North, IntVec2.One).FirstOrDefault(n => n.InBounds(map) && n.Standable(map) && !(n.GetTerrain(map)?.IsWater ?? true) && n.GetFirstBuilding(map) == null);
                    if (!dry.IsValid || dry == IntVec3.Zero || !p.CanReach(dry, PathEndMode.Touch, Danger.Some)) continue;
                    return dry;
                }
                if (p.CanReach(c, PathEndMode.Touch, Danger.Some)) return c;
            }
            return IntVec3.Invalid;
        }

        private static bool Setting(Pawn p) => p?.CurJobDef != null && p.CurJobDef == PeopleEvents4.LanternJob;

        /// <summary>
        /// The boatmen's hour: while lanterns are left to set and it is dusk or night, the next of them to the water; in the
        /// morning their torches taken back, the light lifted, and they go.
        /// </summary>
        public static bool Hour(Visit v, int now)
        {
            if (v.done) return false;
            var boatmen = v.group.Prepend(v.x).Where(p => PeoplesG.Up(p, v.map)).ToList();
            // Their stay ends with the morning (Begin set it so).
            if (boatmen.Count == 0 || now > v.until)
            {
                Finish(v, boatmen);
                return false;
            }
            if (v.stake <= 0) return true;
            int busy = boatmen.Count(Setting);
            foreach (var p in boatmen.Where(p => !Setting(p)))
            {
                if (v.stake - busy <= 0) break;
                var at = NextWater(v, p);
                if (!at.IsValid) break;
                if (PeopleEvents4.Send(p, PeopleEvents4.LanternJob, at, 600)) busy++;
            }
            return true;
        }

        private static Visit VisitOf(Pawn p) =>
            CouncilGameComponent.Instance?.happenings?.visits?.FirstOrDefault(v => v != null && v.kind == Kind && !v.done && (v.x == p || v.group.Contains(p)));

        /// <summary>A lantern set (the boatman's job done): floating on the water, the colony's (Merren's), or a torch of theirs on the bank.</summary>
        public static void Set(Pawn boatman, IntVec3 cell)
        {
            var v = VisitOf(boatman);
            var map = boatman?.Map;
            if (v == null || map == null || v.stake <= 0 || !cell.InBounds(map)) return;
            var def = Floating ?? Torch;
            if (def == null) return;
            try
            {
                var t = ThingMaker.MakeThing(def, def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null);
                t.SetFaction(Floating != null ? Faction.OfPlayer : boatman.Faction);
                var placed = GenSpawn.Spawn(t, cell, map);
                Fuel(placed);
                v.stake--;
                if (Floating != null) FleckMaker.WaterSplash(cell.ToVector3Shifted(), map, 0.8f, 0.3f);
                if (Rand.Chance(0.4f)) PeoplesG.SayLine(boatman, v.incident, "lantern");
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 4: a lantern", e);
            }
        }

        /// <summary>The morning: their torches and light taken back (the floating lanterns stay), and they go home.</summary>
        private static void Finish(Visit v, List<Pawn> boatmen)
        {
            v.done = true;
            var f = PeoplesF.PeopleOf(v);
            var map = v.map;
            if (map != null && f != null)
                foreach (var t in GenRadial.RadialDistinctThingsAround(v.cell, map, 12f, true).Where(t => t.def == Torch && t.Faction == f).ToList())
                    if (!t.Destroyed) t.Destroy();
            if (v.place != null && !v.place.Destroyed && v.place.Faction == f) v.place.Destroy();
            PeoplesG.Home(boatmen);
        }

        /// <summary>
        /// Round 2's knot night at a crossing of theirs: the boatmen resting there set the lanterns on its water too
        /// (DilemmaEffect_KnotRest), and the year's lanterns are theirs.
        /// </summary>
        public static void AtCrossing(HappeningContext ctx)
        {
            var map = ctx?.map;
            if (map == null || ctx.faction == null || !ctx.spot.IsValid) return;
            PeopleEvents4Book.Instance?.NoteLanterns(ctx.faction, GenLocalDate.Year(map));
            var v = Begin(map, ctx.faction, PeoplesG.Party(ctx), ctx.spot, null, PeopleEvents4Math.Lanterns(Lost(map).Count, TakenIn(map)), DefDatabase<IncidentDef>.GetNamedSilentFail(EventDef));
            if (v == null) return;
            Tell.Message(NewsKind.World, Importance.Flavor, map, "CC_PEv4_LanternsAtCrossing".Translate(ctx.faction.Name.Named("FACTION")).Resolve(), new LookTargets(new TargetInfo(ctx.spot, map)),
                MessageTypeDefOf.NeutralEvent);
        }
    }

    /// <summary>
    /// Boatmen of {FACTION} ({X}) at the colony's bank on their knot night, with {COUNT} lanterns for everyone the colony lost
    /// or took in ({DEAD}: some of its dead by name, when it has any).
    /// </summary>
    public class HappeningWorker_KnotLanterns : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) => KnotLanterns.Tonight(map) != null && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = KnotLanterns.Tonight(ctx.map);
            ctx.spot = HappeningWorker_RiverNets.Bank(ctx.map);
            if (ctx.faction == null || !ctx.spot.IsValid) return false;
            var lost = KnotLanterns.Lost(ctx.map);
            ctx.count = PeopleEvents4Math.Lanterns(lost.Count, KnotLanterns.TakenIn(ctx.map));
            ctx.variant = lost.Count;
            ctx.dead = TextUtil.JoinAnd(lost.Take(3).ToList());
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_RiverFolk_Ferryman"), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            for (int i = 0; i < Rand.RangeInclusive(1, 2); i++)
            {
                var p = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_RiverFolk_Fisher"), ctx.faction, ext.ages);
                if (p != null) ctx.group.Add(p);
            }
            ctx.a = KinMemory.Greeter(ctx.faction, ctx.map) ?? PeoplesD.Able(ctx.map).FirstOrDefault();
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return ctx.a != null;
        }

        /// <summary>They walk down to the bank and set their light there, waiting for the answer.</summary>
        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            if (Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, ctx.spot, ext.stayHours.RandomInRange) == null) return false;
            ctx.place = KnotLanterns.BankLight(ctx.map, ctx.spot, ctx.faction);
            return true;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            PeopleEvents.AgeTags(tags, ctx.faction);
            tags.Add(ctx.variant > 0 ? "dead" : "nodead");
            tags.Add(KnotLanterns.Floating != null ? "floating" : "torches");
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option <= 1 ? "set" : "thanks");
            // Sent on: their light lifted from the bank.
            if (option == 2 && ctx.place != null && !ctx.place.Destroyed && ctx.place.Faction == ctx.faction) ctx.place.Destroy();
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The lanterns set on the colony's water (KnotLanterns.Begin), the boatmen resting by the bank until the morning.</summary>
    public class DilemmaEffect_KnotLanterns : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc == null) return;
            sheet.Play((KnotLanterns.Floating != null ? "CC_PEv4_Impact_Floating" : "CC_PEv4_Impact_Torches").Translate(Mathf.Min(PeopleEvents4Math.LanternsSet, hc.count)).Resolve(),
                "CC_PEv4_Impact_LanternsBrief".Translate().Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            if (hc?.map == null || hc.faction == null) return;
            KnotLanterns.Begin(hc.map, hc.faction, PeoplesG.Party(hc), hc.spot, hc.place, hc.count, hc.incident);
        }
    }
}
