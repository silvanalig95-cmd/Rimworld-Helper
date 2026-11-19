using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    // ===================== 65. A mast at a friend's town (the Signal Line) =====================

    /// <summary>
    /// A mast at a friend's town (item 65): a lineman of the Signal Line offers, for a fee, to raise one of the Line's masts at the
    /// town of a notable the colony writes to (World/NeighbourLetters.cs); paid in silver, or in components at their worth. The lineman
    /// walks there (a walking caravan's days, PeopleEvents2Math.RoadDays) and raises it in a day, told when it stands. From then the
    /// colony's letters to that town and its answers take half the days on the road (PeopleEvents7.Relays, as the colony's own relay
    /// mast does; the two together still half, never a quarter), while the Line is at peace with the colony. The mast is lost with the
    /// town. It stands at the town on the world, not on the colony's map, and the words say so.
    /// </summary>
    public static class TownMast
    {
        public const string EventDef = "Rimfolk_Evt_TownMast";

        private static int Now => Find.TickManager.TicksGame;

        /// <summary>The colony's letters to this notable's town go down the Line's wire: a mast up there, the Line at peace.</summary>
        public static bool Relays(Notable n)
        {
            if (n == null || n.town < 0) return false;
            var b = PeopleEvents7Book.Instance;
            return b != null && b.masts.Any(m => m != null && m.raised && !m.done && m.town == n.town) && PeoplesD.Friendly(PeoplesMath.Linesmen) != null;
        }

        /// <summary>A mast there already, or one being raised.</summary>
        public static bool Has(int town) => PeopleEvents7Book.Instance?.masts.Any(m => m != null && !m.done && m.town == town) ?? false;

        /// <summary>
        /// The friend: a notable alive and not at war with the colony whom it has written to, at a town of their own that still stands,
        /// not one of the Line's (its towns are masts already), with no mast of the Line's there; the one written to most, the nearest next.
        /// </summary>
        public static Notable Friend(Map map, Faction line)
        {
            var book = NotableBook.Instance;
            if (book == null || map == null) return null;
            return book.notables.Where(n => n != null && n.Alive && n.town >= 0 && !NotableBook.Hostile(n) && n.faction != line && !Has(n.town) && book.letters.Any(l => l != null && l.notable == n.id))
                .Select(n => (n, town: NotableBook.TownOf(n.town))).Where(x => x.town != null && x.town.Faction != line)
                .OrderByDescending(x => book.letters.Count(l => l != null && l.notable == x.n.id)).ThenBy(x => Find.WorldGrid.ApproxDistanceInTiles(map.Tile, x.town.Tile))
                .Select(x => x.n).FirstOrDefault();
        }

        /// <summary>The fee paid: the record made, the lineman on the road there.</summary>
        public static void Ordered(HappeningContext ctx)
        {
            var b = PeopleEvents7Book.Instance;
            var n = ctx?.note != null && int.TryParse(ctx.note, out int id) ? NotableBook.Instance?.Get(id) : null;
            var town = n != null ? NotableBook.TownOf(n.town) : null;
            if (b == null || n == null || town == null || ctx.map == null) return;
            float tiles = Find.WorldGrid.ApproxDistanceInTiles(ctx.map.Tile, town.Tile);
            b.AddMast(new TownMastRecord
            {
                town = town.ID, townName = town.Label, notable = n.id, notableName = n.Name, faction = ctx.faction, map = ctx.map,
                due = Now + (int)(PeopleEvents7Math.MastDays(PeopleEvents2Math.RoadDays(tiles)) * GenDate.TicksPerDay)
            });
            Arrivals.SendAway(PeoplesG.Party(ctx));
        }

        /// <summary>The events' hour: a mast due stands, told; one whose town is gone is lost with it, told.</summary>
        public static void Watch(int now)
        {
            var b = PeopleEvents7Book.Instance;
            if (b == null) return;
            foreach (var m in b.masts.Where(m => m != null && !m.done).ToList())
            {
                var town = NotableBook.TownOf(m.town);
                if (town == null || town.Destroyed)
                {
                    m.done = true;
                    if (m.raised)
                        Tell.Message(NewsKind.World, Importance.Notable, m.map ?? Homes.All.FirstOrDefault(), "CC_PEv7_TownMastLost".Translate((m.townName ?? "?").Named("TOWN")).Resolve(),
                            LookTargets.Invalid, MessageTypeDefOf.NeutralEvent);
                    continue;
                }
                if (m.raised || now < m.due) continue;
                m.raised = true;
                PeopleEvents7.Goodwill(m.faction, PeopleEvents7Math.MastGoodwill, GoodwillWhy.Paid);
                Tell.Message(NewsKind.World, Importance.Notable, m.map ?? Homes.All.FirstOrDefault(),
                    "CC_PEv7_TownMastUp".Translate((m.townName ?? "?").Named("TOWN"), (m.notableName ?? "?").Named("NAME"), (m.faction?.Name ?? "?").Named("FACTION")).Resolve(),
                    new LookTargets(town), MessageTypeDefOf.PositiveEvent);
            }
        }

        /// <summary>Dev mode: every mast being raised stands now.</summary>
        public static string Soon()
        {
            var b = PeopleEvents7Book.Instance;
            var open = b?.masts.Where(m => m != null && !m.done && !m.raised).ToList();
            if (open == null || open.Count == 0) return "no mast being raised (fire Rimfolk_Evt_TownMast first).";
            foreach (var m in open) m.due = Now;
            return open.Count + " mast(s) stand within the hour.";
        }
    }

    /// <summary>
    /// A lineman of {FACTION} ({X}) at the gate: for {COUNT} silver (or {TALE} components) the Line would raise a mast at {PLACE}, where
    /// {RELATION} lives, and the colony's letters there would take half the days.
    /// </summary>
    public class HappeningWorker_TownMast : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext)
        {
            var line = PeoplesD.Friendly(PeoplesMath.Linesmen);
            return line != null && NotableBook.On && TownMast.Friend(map, line) != null && Colonists(map).Count > 0;
        }

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Linesmen);
            var n = ctx.faction != null ? TownMast.Friend(ctx.map, ctx.faction) : null;
            var town = n != null ? NotableBook.TownOf(n.town) : null;
            if (n == null || town == null) return false;
            float tiles = Find.WorldGrid.ApproxDistanceInTiles(ctx.map.Tile, town.Tile);
            ctx.count = PeopleEvents7Math.MastFee(tiles);
            ctx.thing = ThingDefOf.ComponentIndustrial;
            ctx.variant = PeopleEvents7Math.MastComponents(ctx.count, ThingDefOf.ComponentIndustrial.BaseMarketValue);
            ctx.tale = ctx.variant.ToString();
            ctx.relation = n.Name;
            ctx.placeName = town.Label;
            ctx.note = n.id.ToString();
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_SignalLine_Lineman"), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            ctx.a = KinMemory.Greeter(ctx.faction, ctx.map) ?? PeoplesD.Able(ctx.map).FirstOrDefault();
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            PeopleEvents.AgeTags(tags, ctx.faction);
            if (RelayMast.Working(ctx.map)) tags.Add("mast");
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option <= 1 ? "raise" : "later");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The mast ordered at the friend's town (TownMast.Ordered): the lineman sets off for it.</summary>
    public class DilemmaEffect_TownMast : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override string Unavailable(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            var n = hc?.note != null && int.TryParse(hc.note, out int id) ? NotableBook.Instance?.Get(id) : null;
            return n == null || NotableBook.TownOf(n.town) == null ? "CC_PEv7_TownGone".Translate().Resolve() : null;
        }

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc == null) return;
            sheet.Play("CC_PEv7_Impact_TownMast".Translate(hc.placeName ?? "?").Resolve(), "CC_PEv7_Impact_TownMastBrief".Translate().Resolve());
            sheet.Add(ImpactKind.Other, 1, "CC_PEv7_Impact_TownMastLetters".Translate(hc.relation ?? "?", hc.placeName ?? "?").Resolve(), "CC_PEv7_Impact_TownMastLettersBrief".Translate().Resolve(),
                "pev7townmast");
            if (hc.faction != null) sheet.Goodwill(hc.faction, PeopleEvents7Math.MastGoodwill);
        }

        public override void Apply(DilemmaContext ctx) => TownMast.Ordered(ctx as HappeningContext);
    }
}
