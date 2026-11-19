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
    // ===================== 68. Oil in the ground (the Deepwell Union) =====================

    /// <summary>
    /// Oil in the ground (item 68): an assayer of the Deepwell Union comes to the gate with a driller. With Rimefeller, and its wells
    /// open to the colony (their research done), the assayer has found the colony's oil by Rimefeller's own deposits (the cell its own
    /// well may be placed on, its placing's own rule) and would sink a well there, Rimefeller's own, the colony's: for silver, or for a
    /// year's share (a season's chemfuel collected four times by the union's courier, else its worth in silver). Without Rimefeller the
    /// assayer marks the deep seam nearest the colony for the union's drill instead (the game's deep resources, round F's drill for a
    /// share: its crew comes for it a day later), for a small fee; the colony's best miner may walk the ground with them and learn. The
    /// work at the cell plays out (six hours' drilling for the well, an hour's survey for the mark). One of the events' visits (kind
    /// "pev:well"): x the assayer, group the driller, cell the place, text "well:defName" or "seam", stake 1 for the share.
    /// </summary>
    public static class OilInGround
    {
        public const string Kind = "pev:well";
        public const string OilDef = "Rimfolk_Evt_OilInGround", SeamDef = "Rimfolk_Evt_DeepSeam";
        public const string DrillDef = "Rimfolk_Evt_UnionDrill";

        private static int Now => Find.TickManager.TicksGame;

        private static readonly Dictionary<int, (int hour, IntVec3 cell, ThingDef def)> found = new Dictionary<int, (int, IntVec3, ThingDef)>();

        /// <summary>Rimefeller's well the colony can use: the first of them whose research is done.</summary>
        public static ThingDef Well() =>
            PeopleEvents7Math.Wells.Select(n => DefDatabase<ThingDef>.GetNamedSilentFail(n)).FirstOrDefault(d => d != null && d.category == ThingCategory.Building && d.IsResearchFinished);

        /// <summary>
        /// The colony's oil: the cell nearest its middle where Rimefeller's own well may be placed (its placing reads its deposits), open
        /// ground, not fogged. Looked for once an hour a home.
        /// </summary>
        public static IntVec3 Oil(Map map, out ThingDef well)
        {
            well = null;
            if (map == null) return IntVec3.Invalid;
            int hour = Now / GenDate.TicksPerHour;
            if (found.TryGetValue(map.uniqueID, out var f) && f.hour == hour)
            {
                well = f.def;
                return f.cell;
            }
            var cell = IntVec3.Invalid;
            well = Well();
            var mid = CampSite.ColonyCenter(map);
            if (well != null && mid.IsValid)
                try
                {
                    foreach (var c in GenRadial.RadialCellsAround(mid, PeopleEvents7Math.OilReach, true))
                    {
                        if (!c.InBounds(map) || c.Fogged(map) || c.Roofed(map) || !c.Standable(map) || c.GetFirstBuilding(map) != null) continue;
                        if (!GenConstruct.CanPlaceBlueprintAt(well, c, Rot4.North, map).Accepted) continue;
                        cell = c;
                        break;
                    }
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("peoples' events 7: looking for the oil", e);
                    cell = IntVec3.Invalid;
                }
            if (!cell.IsValid) well = null;
            found[map.uniqueID] = (hour, cell, well);
            return cell;
        }

        private static Visit Of(Pawn x) => CouncilGameComponent.Instance?.happenings?.visits?.FirstOrDefault(v => v != null && v.kind == Kind && !v.done && v.x == x);

        /// <summary>
        /// The answer taken: the assayer and the driller walk to the place and work there (six hours for a well, an hour for a mark); the
        /// colony's miner beside them with the seam's second answer. With the play-outs off, done at once.
        /// </summary>
        public static void Begin(HappeningContext ctx, bool share, bool walk)
        {
            var map = ctx?.map;
            var t = CouncilGameComponent.Instance?.happenings;
            var x = ctx?.stranger;
            if (map == null || t == null || x == null || !ctx.spot.IsValid) return;
            bool well = ctx.thing != null && ctx.thing.category == ThingCategory.Building;
            var crew = PeoplesG.Party(ctx).Where(p => PeoplesG.Up(p, map)).ToList();
            var v = t.AddVisit(Kind, ctx.incident, map, x, walk ? ctx.a : null, crew.Skip(1).ToList(), Now + 12 * GenDate.TicksPerHour);
            v.cell = ctx.spot;
            v.text = well ? "well:" + ctx.thing.defName : "seam";
            v.stake = share ? 1 : 0;
            int ticks = well ? PeopleEvents7Math.SinkTicks : PeopleEvents7Math.MarkTicks;
            if (PlayOuts.On && crew.Count > 0 && x.CanReach(ctx.spot, PathEndMode.Touch, Danger.Some))
            {
                PeoplesD.Gather(crew, ctx.faction, ctx.spot, ticks / (float)GenDate.TicksPerHour + 3f);
                if (PeopleEvents7.Send(x, PeopleEvents7.SinkJob, ctx.spot, ticks))
                {
                    foreach (var p in crew.Skip(1)) PeopleEvents7.Send(p, PeopleEvents7.SinkJob, ctx.spot, ticks);
                    if (walk && ctx.a != null && ctx.a.Spawned && ctx.a.Map == map && !ctx.a.Downed && !ctx.a.Drafted)
                        PeopleEvents7.Send(ctx.a, PeopleEvents7.SinkJob, ctx.spot, Mathf.Min(ticks, 3 * GenDate.TicksPerHour));
                    return;
                }
            }
            if (walk && ctx.a != null) ctx.a.skills?.Learn(SkillDefOf.Mining, PeopleEvents7Math.SinkXp, true);
            Done(v);
        }

        /// <summary>A turn at the place done: the assayer's ends the work; the colony's miner learns from it.</summary>
        public static void Sunk(Pawn by, IntVec3 cell)
        {
            if (by == null) return;
            if (by.Faction != null && by.Faction.IsPlayer)
            {
                by.skills?.Learn(SkillDefOf.Mining, PeopleEvents7Math.SinkXp, true);
                return;
            }
            var v = Of(by);
            if (v != null) Done(v);
        }

        /// <summary>The visit's hour: the assayer kept from it, the work is done there and then.</summary>
        public static bool Hour(Visit v, int now)
        {
            if (v.done) return false;
            if (v.text != null && v.text.StartsWith("drill|")) return DrillCall(v, now);
            var x = v.x;
            if (x != null && x.Spawned && x.CurJobDef == PeopleEvents7.SinkJob && now <= v.until) return true;
            Done(v);
            return false;
        }

        /// <summary>The work done: the well stands there, the colony's (its share noted), or the seam is marked and the drill crew called; told.</summary>
        private static void Done(Visit v)
        {
            if (v.done) return;
            v.done = true;
            var map = v.map;
            var f = v.x?.Faction ?? PeopleEvents5.PeopleOf(v);
            var crew = v.group.Prepend(v.x).Where(p => p != null && p.Spawned).ToList();
            try
            {
                if (v.text != null && v.text.StartsWith("well:"))
                {
                    var def = DefDatabase<ThingDef>.GetNamedSilentFail(v.text.Substring(5));
                    var cell = v.cell;
                    if (def != null && map != null && !GenConstruct.CanPlaceBlueprintAt(def, cell, Rot4.North, map).Accepted) cell = Oil(map, out _);
                    if (def != null && map != null && cell.IsValid)
                    {
                        var well = ThingMaker.MakeThing(def, def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null);
                        well.SetFaction(Faction.OfPlayer);
                        var made = GenSpawn.Spawn(well, cell, map, Rot4.North);
                        FleckMaker.ThrowDustPuff(cell, map, 1.5f);
                        if (crew.Count > 0) PeoplesG.SayLine(crew[0], v.incident, "done");
                        if (v.stake == 1)
                            PeopleEvents7Book.Instance?.AddShare(new ShareRecord
                            {
                                map = map, faction = f, left = PeopleEvents7Math.ShareSeasons, next = Now + PeopleEvents7Math.ShareSeasonDays * GenDate.TicksPerDay
                            });
                        PeopleEvents7.Goodwill(f, PeopleEvents7Math.OilGoodwill, GoodwillWhy.Paid);
                        Tell.Message(NewsKind.Land, Importance.Important, map,
                            (v.stake == 1 ? "CC_PEv7_WellSunkShare" : "CC_PEv7_WellSunk").Translate(made.LabelNoCount.Named("THING"), (f?.Name ?? "?").Named("FACTION"),
                                PeopleEvents7Math.ShareChemfuel.Named("COUNT")).Resolve(), new LookTargets(made), MessageTypeDefOf.PositiveEvent);
                    }
                }
                else if (map != null)
                {
                    var seam = HappeningWorker_UnionDrill.Seam(map, out var ore, out int deep);
                    if (seam.IsValid && ore != null)
                    {
                        if (crew.Count > 0) PeoplesG.SayLine(crew[0], v.incident, "marked");
                        FleckMaker.ThrowMicroSparks(seam.ToVector3Shifted(), map);
                        PeopleEvents7.Goodwill(f, PeopleEvents7Math.OilGoodwill, GoodwillWhy.Paid);
                        Tell.Message(NewsKind.Land, Importance.Notable, map,
                            "CC_PEv7_SeamMarked".Translate(deep.Named("COUNT"), ore.label.Named("ORE"), (f?.Name ?? "?").Named("FACTION")).Resolve(), new LookTargets(seam, map),
                            MessageTypeDefOf.PositiveEvent);
                        // The union's drill crew comes for it a day later (round F's drill for a share).
                        var t = CouncilGameComponent.Instance?.happenings;
                        if (t != null && f != null)
                        {
                            var call = t.AddVisit(Kind, v.incident, map, null, null, null, Now + 4 * GenDate.TicksPerDay);
                            call.text = "drill|" + f.GetUniqueLoadID();
                            call.due = Now + Mathf.RoundToInt(PeopleEvents7Math.DrillAfterDays * GenDate.TicksPerDay);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 7: the well or the seam", e);
            }
            if (crew.Count > 0) Arrivals.SendAway(crew);
        }

        /// <summary>The drill crew's call for a marked seam (a visit with no one at it): at its time, round F's drill for a share comes.</summary>
        public static bool DrillCall(Visit v, int now)
        {
            if (now < v.due) return true;
            if (v.map == null || !Tell.CanAsk || MapFacts.ThreatOn(v.map))
            {
                if (now < v.until) return true;
                v.done = true;
                return false;
            }
            var id = (v.text ?? "").Split('|').ElementAtOrDefault(1);
            var f = Find.FactionManager.AllFactionsListForReading.FirstOrDefault(x => x.GetUniqueLoadID() == id);
            v.done = true;
            if (f != null && PeoplesD.Theirs(f, PeoplesMath.Drillers)) PeopleEvents2.Fire(DrillDef, v.map, f);
            return false;
        }

        /// <summary>The events' hour: a well's season share due is collected (chemfuel from the stores, else its worth in silver), told.</summary>
        public static void WatchShares(int now)
        {
            var b = PeopleEvents7Book.Instance;
            if (b == null) return;
            foreach (var r in b.shares.Where(r => r != null && !r.Done).ToList())
            {
                if (r.map == null || !Find.Maps.Contains(r.map) || r.faction == null || r.faction.defeated || r.faction.HostileTo(Faction.OfPlayer))
                {
                    r.left = 0;
                    continue;
                }
                if (now < r.next) continue;
                r.left--;
                r.next = now + PeopleEvents7Math.ShareSeasonDays * GenDate.TicksPerDay;
                var fuel = ThingDefOf.Chemfuel;
                int silver = Mathf.RoundToInt(PeopleEvents7Math.ShareChemfuel * (fuel?.BaseMarketValue ?? 2.3f));
                string key;
                if (fuel != null && Couriers.TakeFromStores(r.map, fuel, PeopleEvents7Math.ShareChemfuel, r.faction)) key = "CC_PEv7_ShareFuel";
                else if (Couriers.TakeFromStores(r.map, ThingDefOf.Silver, silver, r.faction)) key = "CC_PEv7_ShareSilver";
                else key = "CC_PEv7_ShareLetGo";
                Tell.Message(NewsKind.World, Importance.Notable, r.map,
                    key.Translate((r.faction?.Name ?? "?").Named("FACTION"), PeopleEvents7Math.ShareChemfuel.Named("COUNT"), silver.Named("SILVER"), r.left.Named("LEFT")).Resolve(), LookTargets.Invalid,
                    MessageTypeDefOf.NeutralEvent);
            }
        }
    }

    /// <summary>
    /// An assayer of {FACTION} ({X}) at the gate: they have found the colony's oil at {PLACE}, and would sink Rimefeller's {THING} there for
    /// {COUNT} silver or a year's share. Only with Rimefeller, its wells open to the colony.
    /// </summary>
    public class HappeningWorker_OilInGround : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext)
        {
            var f = PeoplesD.Friendly(PeoplesMath.Drillers);
            return f != null && !UnionStrike.Striking(f) && OilInGround.Oil(map, out _).IsValid && Colonists(map).Count > 0;
        }

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Drillers);
            if (ctx.faction == null || UnionStrike.Striking(ctx.faction)) return false;
            ctx.spot = OilInGround.Oil(ctx.map, out var well);
            if (!ctx.spot.IsValid || well == null) return false;
            ctx.thing = well;
            ctx.count = PeopleEvents7Math.WellPrice(LivingEvents.WealthFactor(ctx.map));
            ctx.variant = PeopleEvents7Math.ShareChemfuel;
            ctx.tale = ctx.variant.ToString();
            ctx.placeName = RegionWordsOf(ctx.map, ctx.spot);
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Deepwell_Assayer"), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            ctx.group.Add(Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Deepwell_Miner"), ctx.faction, ext.ages));
            ctx.group.RemoveAll(p => p == null);
            ctx.a = PeoplesD.Best(ctx.map, SkillDefOf.Mining) ?? PeoplesD.Able(ctx.map).FirstOrDefault();
            return ctx.a != null;
        }

        /// <summary>Where the place lies from the colony, in words ("to the north of the colony").</summary>
        public static string RegionWordsOf(Map map, IntVec3 c)
        {
            var mid = CampSite.ColonyCenter(map);
            if (!mid.IsValid || !c.IsValid) return "CC_PEv7_Here".Translate().Resolve();
            var d = c - mid;
            string dir = Mathf.Abs(d.x) > Mathf.Abs(d.z) ? (d.x > 0 ? "East" : "West") : (d.z > 0 ? "North" : "South");
            return ("CC_PEv7_Dir_" + dir).Translate().Resolve();
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags) => PeopleEvents.AgeTags(tags, ctx.faction);

        public override string Unavailable(HappeningContext ctx, int option)
        {
            if (option <= 1 && ctx.thing != null && !GenConstruct.CanPlaceBlueprintAt(ctx.thing, ctx.spot, Rot4.North, ctx.map).Accepted && !OilInGround.Oil(ctx.map, out _).IsValid)
                return "CC_PEv7_OilGone".Translate().Resolve();
            return null;
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option <= 1 ? "sink" : "later");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>
    /// An assayer of {FACTION} ({X}) at the gate: the colony's deepest ground at {PLACE} holds a seam, and for {COUNT} silver they would mark
    /// it for the union's drill. Without Rimefeller (or its wells not yet open to the colony).
    /// </summary>
    public class HappeningWorker_DeepSeam : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext)
        {
            var f = PeoplesD.Friendly(PeoplesMath.Drillers);
            return f != null && !UnionStrike.Striking(f) && !OilInGround.Oil(map, out _).IsValid && HappeningWorker_UnionDrill.Seam(map, out _, out _).IsValid && Colonists(map).Count > 0;
        }

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Drillers);
            if (ctx.faction == null || UnionStrike.Striking(ctx.faction)) return false;
            ctx.spot = HappeningWorker_UnionDrill.Seam(ctx.map, out var ore, out int deep);
            if (!ctx.spot.IsValid || ore == null) return false;
            ctx.thing = ore;
            ctx.count = PeopleEvents7Math.SeamFee;
            ctx.variant = deep;
            ctx.tale = deep.ToString();
            ctx.placeName = HappeningWorker_OilInGround.RegionWordsOf(ctx.map, ctx.spot);
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Deepwell_Assayer"), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            ctx.a = PeoplesD.Best(ctx.map, SkillDefOf.Mining, p => !p.WorkTypeIsDisabled(WorkTypeDefOf.Mining)) ?? PeoplesD.Able(ctx.map).FirstOrDefault();
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            PeopleEvents.AgeTags(tags, ctx.faction);
            if (ctx.map.deepResourceGrid.AnyActiveDeepScannersOnMap()) tags.Add("scanned");
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option <= 1 ? "mark" : "later");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The well sunk or the seam marked (OilInGround.Begin): <see cref="share"/> the well for a year's share, <see cref="walk"/> the colony's miner beside them.</summary>
    public class DilemmaEffect_OilInGround : DilemmaEffect
    {
        public bool share, walk;

        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc?.thing == null) return;
            if (hc.thing.category == ThingCategory.Building)
            {
                sheet.Play("CC_PEv7_Impact_Well".Translate(hc.thing.label, hc.placeName ?? "?").Resolve(), "CC_PEv7_Impact_WellBrief".Translate().Resolve());
                sheet.Goods(1, hc.thing, 1);
                if (share)
                    sheet.Add(ImpactKind.Goods, -1, "CC_PEv7_Impact_WellShare".Translate(PeopleEvents7Math.ShareChemfuel, PeopleEvents7Math.ShareSeasons).Resolve(), "CC_PEv7_Impact_WellShareBrief".Translate().Resolve(),
                        "pev7wellshare");
            }
            else
            {
                sheet.Play("CC_PEv7_Impact_Seam".Translate(hc.thing.label, hc.placeName ?? "?").Resolve(), "CC_PEv7_Impact_SeamBrief".Translate().Resolve());
                if (walk && hc.a != null)
                    sheet.Add(ImpactKind.Skill, 1, "CC_PEv7_Impact_SeamWalk".Translate(hc.a.LabelShort).Resolve(), "CC_PEv7_Impact_SeamWalkBrief".Translate().Resolve(), "pev7seamwalk");
            }
            if (hc.faction != null) sheet.Goodwill(hc.faction, PeopleEvents7Math.OilGoodwill);
        }

        public override void Apply(DilemmaContext ctx) => OilInGround.Begin(ctx as HappeningContext, share, walk);
    }
}
