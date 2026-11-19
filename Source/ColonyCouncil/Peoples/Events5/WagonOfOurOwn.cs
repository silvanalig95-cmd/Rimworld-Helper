using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace ColonyCouncil
{
    // ===================== 45. A wagon of our own (the Wheel) =====================

    /// <summary>
    /// A wagon of our own (item 45, only with Vehicle Framework and Vanilla Vehicles Expanded): a fixer of the Wheel builds the
    /// colony a vehicle of its age at the gate, from the colony's own stores (wood and some steel for the wagon before the
    /// industrial age; steel and components for a small truck after: PeopleEvents5Math.WagonGoods), over a day: the goods are
    /// carried out to them and set down where they build, they work at it in turns, sparks flying, and at the day's end the
    /// goods are gone and the vehicle stands there, the colony's (Vehicle Framework's own way of making one, as the
    /// boat-builders' dinghy). The colony's crafter may work beside them and learn. Without those mods it never comes.
    /// </summary>
    public static class WagonOfOurOwn
    {
        public const string Kind = "pev:wagon";
        public const string EventDef = "Rimfolk_Evt_WagonOfOurOwn";

        private static int Now => Find.TickManager.TicksGame;

        /// <summary>The vehicle for the colony's age, when Vehicle Framework and Vanilla Vehicles Expanded can make it.</summary>
        public static ThingDef Vehicle(int age) => PeopleEvents5Math.WagonDefs(age).Select(BoatBuilders.Vehicle).FirstOrDefault(d => d != null);

        /// <summary>What it is made of, by the colony's age, as defs that exist.</summary>
        public static List<(ThingDef def, int count)> Goods(int age) =>
            PeopleEvents5Math.WagonGoods(age).Select(g => (DefDatabase<ThingDef>.GetNamedSilentFail(g.def), g.count)).Where(g => g.Item1 != null).ToList();

        /// <summary>Where they build: open ground under the sky by the gate, room for a vehicle round it.</summary>
        public static IntVec3 Site(Map map)
        {
            var gate = PeoplesD.GateSpot(map);
            if (map == null || !gate.HasValue) return IntVec3.Invalid;
            foreach (var c in GenRadial.RadialCellsAround(gate.Value, 16f, true))
            {
                if (c.DistanceTo(gate.Value) < 4f) continue;
                var rect = CellRect.CenteredOn(c, 2);
                if (rect.Cells.All(x => x.InBounds(map) && x.Standable(map) && !x.Roofed(map) && !x.Fogged(map) && x.GetEdifice(map) == null && x.GetFirstItem(map) == null
                                        && !(x.GetTerrain(map)?.IsWater ?? true) && x.GetZone(map) == null))
                    return c;
            }
            return IntVec3.Invalid;
        }

        private static List<Pawn> Crew(Visit v) => v.group.Prepend(v.x).Where(p => PeoplesG.Up(p, v.map)).ToList();

        /// <summary>The build begins: the visit for the day at the gate, the crew kept there by a lord of their own; <paramref name="lesson"/>: the colony's crafter beside them.</summary>
        public static void Begin(HappeningContext ctx, bool lesson)
        {
            var map = ctx?.map;
            var t = CouncilGameComponent.Instance?.happenings;
            var crew = PeoplesG.Party(ctx).Where(p => PeoplesG.Up(p, map)).ToList();
            var site = Site(map);
            if (map == null || t == null || crew.Count == 0 || !site.IsValid || ctx.thing == null) return;
            int hours = PeopleEvents5Math.WagonHours + 18;
            var v = t.AddVisit(Kind, ctx.incident, map, crew[0], lesson ? ctx.a : null, crew.Skip(1).ToList(), Now + hours * GenDate.TicksPerHour);
            v.cell = site;
            v.text = ctx.thing.defName;
            v.due = -1;
            v.stake = PeopleEvents2.ColonyAge;
            Lords.Leave(crew);
            LordMaker.MakeNewLord(ctx.faction, new LordJob_VisitOnErrand(ctx.faction, site, hours * GenDate.TicksPerHour), map, crew);
        }

        /// <summary>The goods they carry (handed over by the colony) set down where they build, theirs to use: the work can begin.</summary>
        private static bool SetDown(Visit v, List<Pawn> crew)
        {
            var goods = Goods(v.stake);
            bool any = false;
            foreach (var p in crew)
            {
                var pack = p.inventory?.innerContainer;
                if (pack == null) continue;
                foreach (var thing in pack.Where(x => goods.Any(g => g.def == x.def)).ToList())
                    if (pack.TryDrop(thing, v.cell, v.map, ThingPlaceMode.Near, out var dropped))
                    {
                        dropped.SetForbidden(true, false);
                        any = true;
                    }
            }
            return any;
        }

        /// <summary>Of the goods, what lies where they build.</summary>
        private static List<Thing> Pile(Visit v)
        {
            var goods = Goods(v.stake);
            return GenRadial.RadialDistinctThingsAround(v.cell, v.map, 4f, true).Where(x => x.def.category == ThingCategory.Item && goods.Any(g => g.def == x.def)).ToList();
        }

        /// <summary>
        /// The visit's hour: once the goods are set down, a day's work in turns at the frame (the crafter beside them learning,
        /// if sent); at its end the goods are used up and the vehicle stands there, the colony's.
        /// </summary>
        public static bool Hour(Visit v, int now)
        {
            if (v.done) return false;
            var crew = Crew(v);
            if (crew.Count == 0 || now > v.until)
            {
                Finish(v, crew, false);
                return false;
            }
            if (v.due < 0)
            {
                if (!SetDown(v, crew) && Pile(v).Count == 0) return true;
                v.due = now + PeopleEvents5Math.WagonHours * GenDate.TicksPerHour;
                PeoplesG.SayLine(crew[0], v.incident, "begin");
            }
            if (now >= v.due)
            {
                Finish(v, crew, true);
                return false;
            }
            if (v.map.dangerWatcher.DangerRating != StoryDanger.None) return true;
            foreach (var p in crew.Where(p => p.CurJobDef != PeopleEvents5.FrameJob))
                PeopleEvents5.Send(p, PeopleEvents5.FrameJob, v.cell, PeopleEvents5Math.WagonTurnTicks);
            int hour = GenLocalDate.HourOfDay(v.map);
            if (v.a != null && PeoplesG.Up(v.a, v.map) && hour >= 8 && hour <= 18)
            {
                // The crafter at their side through the working hours, learning as they go.
                PeopleWorkOn.Bring(v.a, new LocalTargetInfo(v.cell), "beside", GenDate.TicksPerHour + 600);
                v.a.skills?.Learn(SkillDefOf.Crafting, PeopleEvents5Math.WagonLessonXp / 12f, true);
            }
            return true;
        }

        /// <summary>A turn at the frame done: sparks, and now and then a word.</summary>
        public static void Worked(Pawn by, IntVec3 cell)
        {
            if (by?.Map != null) FleckMaker.ThrowMicroSparks(cell.ToVector3Shifted(), by.Map);
            if (Rand.Chance(0.15f)) PeoplesG.SayLine(by, DefDatabase<IncidentDef>.GetNamedSilentFail(EventDef), "work");
        }

        private static void Finish(Visit v, List<Pawn> crew, bool built)
        {
            v.done = true;
            PeopleWorkOn.Release(v.a);
            var f = PeopleEvents5.PeopleOf(v);
            var def = BoatBuilders.Vehicle(v.text ?? "");
            Thing made = null;
            if (built && def != null)
            {
                made = BoatBuilders.MakeVehicle(def, Faction.OfPlayer);
                if (made != null)
                    try
                    {
                        foreach (var t in Pile(v)) t.Destroy();
                        GenSpawn.Spawn(made, v.cell, v.map, Rot4.North);
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("peoples' events 5: the wagon set down", e);
                        made = null;
                    }
            }
            if (made != null)
            {
                if (f != null && f.HasGoodwill && !f.IsPlayer && !f.HostileTo(Faction.OfPlayer)) WorldSituationWorker.Goodwill(f, PeopleEvents5Math.WagonGoodwill, GoodwillWhy.Helped);
                if (crew.Count > 0) PeoplesG.SayLine(crew[0], v.incident, "done");
                Tell.Message(NewsKind.Land, Importance.Notable, v.map, "CC_PEv5_WagonBuilt".Translate(made.LabelNoCount.Named("THING"), (f?.Name ?? "?").Named("FACTION")).Resolve(),
                    new LookTargets(made), MessageTypeDefOf.PositiveEvent);
            }
            else if (v.map != null)
            {
                // Never finished: what was set down stays for the colony.
                foreach (var t in Pile(v)) t.SetForbidden(false, false);
                Tell.Message(NewsKind.Land, Importance.Notable, v.map, "CC_PEv5_WagonUnfinished".Translate((f?.Name ?? "?").Named("FACTION")).Resolve(), LookTargets.Invalid,
                    MessageTypeDefOf.NeutralEvent);
            }
            PeoplesG.Home(crew);
        }
    }

    /// <summary>A fixer of {FACTION} ({X}) at the gate: they would build the colony a {THING} at the gate over a day, from its stores ({TALE}), for {COUNT} silver.</summary>
    public class HappeningWorker_WagonOfOurOwn : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            PeoplesD.Friendly(PeoplesMath.Salvagers) != null && WagonOfOurOwn.Vehicle(PeopleEvents2.ColonyAge) != null && WagonOfOurOwn.Site(map).IsValid
            && !(CouncilGameComponent.Instance?.happenings?.visits?.Any(v => v != null && v.kind == WagonOfOurOwn.Kind && !v.done) ?? false) && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Salvagers);
            int age = PeopleEvents2.ColonyAge;
            ctx.thing = WagonOfOurOwn.Vehicle(age);
            if (ctx.faction == null || ctx.thing == null) return false;
            ctx.count = PeopleEvents5Math.WagonFee(age);
            ctx.tale = TextUtil.JoinAnd(WagonOfOurOwn.Goods(age).Select(g => VarietyUtil.Bundle(g.def, g.count)).ToList());
            ctx.stranger = Arrivals.Make(PeoplesD.TraderKind(ctx.faction), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            var hand = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Wheel_Hand"), ctx.faction, ext.ages);
            if (hand != null) ctx.group.Add(hand);
            StorytellerEvents.Skilled(ctx.stranger, SkillDefOf.Crafting, new IntRange(12, 18));
            ctx.a = PeoplesD.Best(ctx.map, SkillDefOf.Crafting) ?? PeoplesD.Able(ctx.map).FirstOrDefault();
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            PeopleEvents.AgeTags(tags, ctx.faction);
            tags.Add(PeopleEvents2.ColonyAge <= 1 ? "wagon" : "truck");
        }

        public override string Unavailable(HappeningContext ctx, int option) =>
            option <= 1 && !WagonOfOurOwn.Site(ctx.map).IsValid ? "CC_PEv5_NoWagonRoom".Translate().Resolve() : null;

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option <= 1 ? "build" : "later");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The colony's goods for it (by its age) carried out to the fixer at the gate, the round's own carry for each (PlayOut/: the carry).</summary>
    public class DilemmaEffect_WagonGoods : DilemmaEffect
    {
        public override bool ChanceEach => false;

        private static List<DilemmaEffect_CarryOut> Carries() =>
            WagonOfOurOwn.Goods(PeopleEvents2.ColonyAge).Select(g => new DilemmaEffect_CarryOut { what = g.def.defName, count = g.count, steward = false }).ToList();

        public override string Unavailable(DilemmaContext ctx) => Carries().Select(c => c.Unavailable(ctx)).FirstOrDefault(r => r != null);

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            foreach (var c in Carries()) c.Describe(ctx, sheet);
        }

        public override void Apply(DilemmaContext ctx)
        {
            foreach (var c in Carries()) c.Apply(ctx);
        }
    }

    /// <summary>The day's build at the gate (WagonOfOurOwn.Begin); <see cref="lesson"/>: the colony's crafter beside them.</summary>
    public class DilemmaEffect_WagonBuild : DilemmaEffect
    {
        public bool lesson;

        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc?.thing == null) return;
            sheet.Play("CC_PEv5_Impact_Wagon".Translate(hc.thing.label).Resolve(), "CC_PEv5_Impact_WagonBrief".Translate().Resolve());
            sheet.Goods(1, hc.thing, 1);
            if (lesson && hc.a != null) sheet.Add(ImpactKind.Skill, 1, "CC_PEv5_Impact_WagonLesson".Translate(hc.a.LabelShort).Resolve(), "CC_PEv5_Impact_WagonLessonBrief".Translate().Resolve(), "pev5wagonlesson");
            if (hc.faction != null) sheet.Goodwill(hc.faction, PeopleEvents5Math.WagonGoodwill);
        }

        public override void Apply(DilemmaContext ctx) => WagonOfOurOwn.Begin(ctx as HappeningContext, lesson);
    }
}
