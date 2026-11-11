using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace ColonyCouncil
{
    // ===================== Goods held in trust (round 1's shared shape: the league's crates, item 1; later the society's books, item 20) =====================

    /// <summary>
    /// A sealed crate left in the colony's care (Defs/Misc/Rimfolk_PeopleEvents_Misc.xml): what it holds ("def:count;..."),
    /// whose trust it belongs to. Opened by the colony (its own button, asked twice), sold to a trader, or destroyed, it says
    /// so to the trust's record as it happens; burned by harm (a raid's fire) is told at once, and forgiven at the fetch.
    /// </summary>
    public class TrustCrate : ThingWithComps
    {
        public string goods;
        public int trust;

        public TrustRecord Record => PeopleEventsBook.Instance?.trusts?.FirstOrDefault(r => r != null && r.id == trust);

        /// <summary>Still held in trust: its record open, its fate not yet noted.</summary>
        public bool Held => Record is TrustRecord r && !r.done && r.FateOf(thingIDNumber) == null;

        public List<(ThingDef def, int count)> Contents()
        {
            var list = new List<(ThingDef, int)>();
            foreach (var part in (goods ?? "").Split(';'))
            {
                var bits = part.Split(':');
                if (bits.Length != 2 || !int.TryParse(bits[1], out int n) || n <= 0) continue;
                var def = DefDatabase<ThingDef>.GetNamedSilentFail(bits[0]);
                if (def != null) list.Add((def, n));
            }
            return list;
        }

        public float Worth => Contents().Sum(c => c.def.BaseMarketValue * c.count);

        public override void PreTraded(TradeAction action, Pawn playerNegotiator, ITrader trader)
        {
            base.PreTraded(action, playerNegotiator, trader);
            if (action == TradeAction.PlayerSells && Held) PeopleTrust.Noted(this, PeopleEventsMath.Sold);
        }

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            bool held = Held;
            var map = MapHeld;
            var at = PositionHeld;
            base.Destroy(mode);
            if (!held) return;
            if (mode == DestroyMode.KillFinalize)
            {
                PeopleTrust.Noted(this, PeopleEventsMath.Burned);
                var r = Record;
                if (map != null)
                    Tell.Message(NewsKind.World, Importance.Notable, map, "CC_PEv_CrateBurned".Translate((r?.faction?.Name ?? "?").Named("FACTION")).Resolve(),
                        new LookTargets(at, map), MessageTypeDefOf.NegativeEvent);
            }
            else PeopleTrust.Noted(this, PeopleEventsMath.Gone);
        }

        public override string GetInspectString()
        {
            string s = base.GetInspectString();
            var r = Record;
            if (r == null || r.done) return s;
            string held = "CC_PEv_CrateHeld".Translate((r.faction?.Name ?? "?").Named("FACTION"), (r.master ?? "?").Named("MASTER")).Resolve();
            return s.NullOrEmpty() ? held : s + "\n" + held;
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (var g in base.GetGizmos()) yield return g;
            if (!Spawned || !Held) yield break;
            yield return new Command_Action
            {
                defaultLabel = "CC_PEv_BreakSeal".Translate(),
                defaultDesc = "CC_PEv_BreakSealDesc".Translate((Record?.faction?.Name ?? "?").Named("FACTION")),
                icon = def.uiIcon,
                action = () => Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("CC_PEv_BreakSealConfirm".Translate((Record?.faction?.Name ?? "?").Named("FACTION")), Open, true))
            };
        }

        /// <summary>The seal broken: what it held spills out where it stood, and the book will say so.</summary>
        public void Open()
        {
            if (!Spawned || !Held) return;
            var map = Map;
            var at = Position;
            PeopleTrust.Noted(this, PeopleEventsMath.Opened);
            foreach (var t in Contents().SelectMany(c => Couriers.Make(c.def, c.count))) GenPlace.TryPlaceThing(t, at, map, ThingPlaceMode.Near);
            Destroy(DestroyMode.Vanish);
            Tell.Message(NewsKind.World, Importance.Notable, map, "CC_PEv_CrateOpened".Translate().Resolve(), new LookTargets(at, map), MessageTypeDefOf.NegativeEvent);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref goods, "goods");
            Scribe_Values.Look(ref trust, "trust");
        }
    }

    /// <summary>
    /// Goods held in trust: a people leaves things in the colony's care until someone of theirs fetches them (the record in
    /// PeopleEventsBook, its fetch one of the events' visits). At the fetch the book's verdict (PeopleEventsMath.Verdict):
    /// good ink when every crate is there (one burned by harm forgiven), the fee paid and a year of better terms with every
    /// people of that calling; red ink for any opened, sold or gone, a grudge and no caravans of theirs for a season.
    /// </summary>
    public static class PeopleTrust
    {
        public const string VisitKind = "pev:trust";

        public static ThingDef CrateDef => DefDatabase<ThingDef>.GetNamedSilentFail("Rimfolk_TrustCrate");

        private static int Now => Find.TickManager.TicksGame;

        /// <summary>A trust still open with a people of this calling.</summary>
        public static bool Open(string calling) => PeopleEventsBook.Instance?.trusts?.Any(r => r != null && !r.done && Peoples.CallingOf(r.faction) == calling) ?? false;

        public static void Noted(TrustCrate c, string fate) => c?.Record?.Note(c.thingIDNumber, fate);

        /// <summary>A crate of these goods, sealed and theirs in trust.</summary>
        public static TrustCrate MakeCrate(string goods)
        {
            var def = CrateDef;
            if (def == null) return null;
            var c = ThingMaker.MakeThing(def) as TrustCrate;
            if (c != null) c.goods = goods;
            return c;
        }

        /// <summary>The goods of the league's load, crate by crate, by its age.</summary>
        public static List<string> Load(int age, int n)
        {
            var list = new List<string>();
            int start = Rand.Range(0, 5);
            for (int i = 0; i < n; i++)
            {
                var (def, count) = PeopleEventsMath.CrateGoods(age, start + i);
                if (DefDatabase<ThingDef>.GetNamedSilentFail(def) == null) (def, count) = ("Cloth", 50);
                list.Add(def + ":" + count);
            }
            return list;
        }

        public static float Worth(IEnumerable<string> load) => load.Sum(g =>
        {
            var bits = g.Split(':');
            var def = DefDatabase<ThingDef>.GetNamedSilentFail(bits[0]);
            return def == null || bits.Length < 2 || !int.TryParse(bits[1], out int n) ? 0f : def.BaseMarketValue * n;
        });

        /// <summary>A stockpile's cell nearest the gate (where the porters set the crates down), else the gate.</summary>
        public static IntVec3 Stores(Map map)
        {
            var gate = PeoplesD.GateSpot(map) ?? CampSite.ColonyCenter(map);
            var cells = map.zoneManager.AllZones.OfType<Zone_Stockpile>().SelectMany(z => z.Cells).Where(c => c.Standable(map) && c.GetFirstItem(map) == null);
            var best = gate.IsValid ? cells.OrderBy(c => c.DistanceToSquared(gate)).FirstOrDefault() : cells.FirstOrDefault();
            return best.IsValid && best != IntVec3.Zero ? best : gate;
        }

        /// <summary>
        /// The crates left: made, carried in by the caravan's porters (by the lander's pods for a league of the spacer age) and
        /// set down by the colony's stores; the record kept; the fetch one of the events' visits. <paramref name="free"/>: kept
        /// for nothing, as friends.
        /// </summary>
        public static TrustRecord Take(HappeningContext ctx, List<string> load, bool free)
        {
            var map = ctx?.map;
            var book = PeopleEventsBook.Instance;
            var f = ctx?.faction;
            if (map == null || book == null || f == null || load.NullOrEmpty()) return null;
            var crates = load.Select(MakeCrate).Where(c => c != null).ToList();
            if (crates.Count == 0) return null;
            var rec = new TrustRecord
            {
                id = book.nextTrust++, faction = f, map = map, left = Now, due = Now + Rand.RangeInclusive(PeopleEventsMath.DueMinDays, PeopleEventsMath.DueMaxDays) * GenDate.TicksPerDay,
                fee = free ? 0 : PeopleEventsMath.TrustFee(Worth(load)), master = ctx.stranger?.LabelShort, means = PeopleEventsMath.Means(PeopleEvents.AgeOf(f))
            };
            foreach (var c in crates)
            {
                c.trust = rec.id;
                rec.crates.Add(c);
                rec.ids.Add(c.thingIDNumber);
            }
            book.trusts.Add(rec);
            while (book.trusts.Count > PeopleEventsMath.MaxTrusts && book.trusts.Any(r => r.done)) book.trusts.Remove(book.trusts.First(r => r.done));
            var at = Stores(map);
            var things = crates.Cast<Thing>().ToList();
            try
            {
                if (rec.means == "lander" && at.IsValid) DropPodUtility.DropThingsNear(at, map, things, 110, false, false, true, false);
                else Couriers.Loot(things, map, ctx.stranger != null && ctx.stranger.Spawned ? ctx.stranger.Position : at, f, PeoplesG.Up(ctx.stranger, map) ? ctx.stranger : null, at);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events: the crates carried in", e);
                foreach (var t in things.Where(t => !t.Spawned && !t.Destroyed)) GenPlace.TryPlaceThing(t, at, map, ThingPlaceMode.Near);
            }
            var v = CouncilGameComponent.Instance?.happenings?.AddVisit(VisitKind, ctx.incident, map, null, null, null, rec.due + 3 * GenDate.TicksPerDay);
            if (v != null) v.stake = rec.id;
            FactionMemory.Note(f, "traded", "CC_PEv_TrustName".Translate().Resolve());
            return rec;
        }

        /// <summary>The fetch's hour: waiting until it's due, then the second caravan; the trust lapses when their people can no longer come.</summary>
        public static bool Hour(Visit v, int now)
        {
            var rec = PeopleEventsBook.Instance?.trusts?.FirstOrDefault(r => r != null && r.id == v.stake);
            if (rec == null || rec.done) return false;
            if (now < rec.due) return true;
            var map = rec.map ?? v.map;
            if (map == null || rec.faction == null || rec.faction.defeated || rec.faction.HostileTo(Faction.OfPlayer))
            {
                Lapse(rec, map);
                return false;
            }
            // Not into a fight: the caravan waits on the road a while.
            if (map.dangerWatcher.DangerRating != StoryDanger.None && now < rec.due + 2 * GenDate.TicksPerDay) return true;
            Fetch(rec, map, v.incident);
            return false;
        }

        private static void Lapse(TrustRecord rec, Map map)
        {
            rec.done = true;
            rec.ink = "lapsed";
            if (map != null)
                Tell.Message(NewsKind.World, Importance.Notable, map, "CC_PEv_TrustLapsed".Translate((rec.faction?.Name ?? "?").Named("FACTION")).Resolve(), LookTargets.Invalid,
                    MessageTypeDefOf.NeutralEvent);
        }

        /// <summary>
        /// The second caravan at the gate: every crate counted (still on this map, opened, sold, burned, gone), the book's
        /// verdict, the crates there carried out to them, the fee paid for good ink, and the terms that follow.
        /// </summary>
        public static void Fetch(TrustRecord rec, Map map, IncidentDef incident)
        {
            var f = rec.faction;
            int total = rec.ids.Count, opened = 0, sold = 0, burned = 0, present = 0;
            var here = rec.crates.Where(c => c != null && !c.Destroyed && c.MapHeld == map && (c.Spawned || c.ParentHolder is Pawn_CarryTracker || c.ParentHolder is Pawn_InventoryTracker)).ToList();
            foreach (int id in rec.ids)
            {
                string fate = rec.FateOf(id);
                if (fate == PeopleEventsMath.Opened) opened++;
                else if (fate == PeopleEventsMath.Sold) sold++;
                else if (fate == PeopleEventsMath.Burned) burned++;
                else if (here.Any(c => c.thingIDNumber == id)) present++;
            }
            var ink = PeopleEventsMath.Verdict(total, present, burned, opened, sold);
            rec.done = true;
            rec.ink = ink == PeopleEventsMath.Ink.Good ? "good" : "red";
            // The porters who fetch them, walking in to the gate.
            var porters = new List<Pawn>();
            try
            {
                for (int i = 0; i < Mathf.Clamp(Mathf.CeilToInt(present / 3f), 1, 3); i++)
                {
                    var p = Arrivals.Make(i == 0 ? PeoplesD.TraderKind(f) : PeoplesD.KindNamed(f, "Rimfolk_League_Wayfarer"), f, new FloatRange(20f, 55f));
                    if (p != null) porters.Add(p);
                }
                if (porters.Count == 0 || Arrivals.Visit(porters, map, f, PeoplesD.GateSpot(map), 10f) == null) porters.Clear();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events: the fetch's porters", e);
                porters.Clear();
            }
            var x = porters.FirstOrDefault();
            // The crates there go out to them; with nobody come, they are taken from the stores as an errand's goods are.
            if (present > 0)
            {
                var ctx = new HappeningContext { map = map, faction = f, stranger = x, incident = incident, count = present };
                // apart: a colonist carries the crates out to the league's porter at the gate, who takes them.
                new DilemmaEffect_CarryOut { what = "Rimfolk_TrustCrate", count = present, steward = false, who = PlayWho.None, label = "CC_PEv_CratesLabel".Translate().Resolve() }.Apply(ctx);
            }
            var book = PeopleEventsBook.Instance;
            string text;
            if (ink == PeopleEventsMath.Ink.Good)
            {
                if (rec.fee > 0) Couriers.PaySilver(map, x != null && x.Spawned ? x.Position : IntVec3.Invalid, rec.fee, f, x);
                if (book != null) book.goodInkUntil = Find.TickManager.TicksGame + PeopleEventsMath.GoodInkDays * GenDate.TicksPerDay;
                if (f.HasGoodwill) WorldSituationWorker.Goodwill(f, rec.fee > 0 ? PeopleEventsMath.GoodInkGoodwill : PeopleEventsMath.GoodInkGoodwill + PeopleEventsMath.FreeGoodwill);
                FactionMemory.Note(f, "keptWord", "CC_PEv_TrustName".Translate().Resolve());
                text = (burned > 0 ? "CC_PEv_InkGoodForgiven" : rec.fee > 0 ? "CC_PEv_InkGood" : "CC_PEv_InkGoodFree").Translate(f.Name.Named("FACTION"), total.Named("TOTAL"),
                    burned.Named("BURNED"), rec.fee.Named("FEE"), (x?.LabelShort ?? rec.master ?? "?").Named("X")).Resolve();
            }
            else
            {
                if (book != null) book.redInkUntil = Find.TickManager.TicksGame + PeopleEventsMath.RedInkDays * GenDate.TicksPerDay;
                if (f.HasGoodwill) WorldSituationWorker.Goodwill(f, PeopleEventsMath.RedInkGoodwill);
                FactionMemory.Note(f, "robbed", "CC_PEv_TrustName".Translate().Resolve(), 12);
                var parts = new List<string>();
                if (opened > 0) parts.Add("CC_PEv_InkOpened".Translate(opened).Resolve());
                if (sold > 0) parts.Add("CC_PEv_InkSold".Translate(sold).Resolve());
                int gone = total - present - opened - sold - burned;
                if (gone > 0) parts.Add("CC_PEv_InkGone".Translate(gone).Resolve());
                if (burned > 0) parts.Add("CC_PEv_InkBurned".Translate(burned).Resolve());
                text = "CC_PEv_InkRed".Translate(f.Name.Named("FACTION"), total.Named("TOTAL"), TextUtil.JoinAnd(parts).Named("WHAT"), (x?.LabelShort ?? rec.master ?? "?").Named("X")).Resolve();
            }
            if (x != null) PeoplesG.SayLine(x, incident, ink == PeopleEventsMath.Ink.Good ? "goodink" : "redink");
            Tell.Letter(NewsKind.World, Importance.Notable, map, (ink == PeopleEventsMath.Ink.Good ? "CC_PEv_InkGoodTitle" : "CC_PEv_InkRedTitle").Translate().Resolve(), text,
                ink == PeopleEventsMath.Ink.Good ? LetterDefOf.PositiveEvent : LetterDefOf.NegativeEvent, x != null && x.Spawned ? new LookTargets(x) : LookTargets.Invalid);
            CouncilGameComponent.Instance?.AddChronicle(new ChronicleEntry(ChronicleKind.World, text, null));
        }

        /// <summary>Good ink: the price offset every people of the traders' calling gives the colony, while it lasts.</summary>
        public static float Terms(Faction f)
        {
            var b = PeopleEventsBook.Instance;
            if (b == null || f == null || f.IsPlayer || b.goodInkUntil < 0 || Find.TickManager.TicksGame > b.goodInkUntil) return 0f;
            return Peoples.CallingOf(f) == PeoplesMath.Traders ? PeopleEventsMath.GoodInkTerms : 0f;
        }

        /// <summary>Red ink: no caravan of the traders' calling comes while it lasts.</summary>
        public static bool RedInk(Faction f)
        {
            var b = PeopleEventsBook.Instance;
            return b != null && f != null && b.redInkUntil >= 0 && Find.TickManager.TicksGame <= b.redInkUntil && Peoples.CallingOf(f) == PeoplesMath.Traders;
        }
    }

    // ===================== 1. Crates held in trust (the road league) =====================

    /// <summary>
    /// A league caravan with a pack beast gone lame (a broken truck for an industrial league, a lander short of lift for a
    /// spacer one: {TALE}'s way) asks to leave {COUNT} sealed crates in the colony's stores until a second caravan fetches
    /// them; its porters carry them in and the master writes the colony into the Waystone Book. For the fee ({THING}) at the
    /// fetch, or for nothing, as friends.
    /// </summary>
    public class HappeningWorker_LeagueTrust : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            PeoplesD.Friendly(PeoplesMath.Traders) != null && !PeopleTrust.Open(PeoplesMath.Traders) && PeopleTrust.CrateDef != null
            && map.zoneManager.AllZones.OfType<Zone_Stockpile>().Any() && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Traders);
            if (ctx.faction == null) return false;
            int age = PeopleEvents.AgeOf(ctx.faction);
            ctx.count = PeopleEventsMath.Crates(Rand.Value);
            // The load, crate by crate, kept for the answer; the fee is a tenth of its worth.
            var goods = PeopleTrust.Load(age, ctx.count);
            ctx.note = string.Join("|", goods);
            ctx.variant = PeopleEventsMath.TrustFee(PeopleTrust.Worth(goods));
            ctx.tale = ("CC_PEv_Means_" + PeopleEventsMath.Means(age)).Translate().Resolve();
            ctx.stranger = Arrivals.Make(PeoplesD.TraderKind(ctx.faction), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            for (int i = 0; i < 2; i++) ctx.group.Add(Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_League_Wayfarer"), ctx.faction, ext.ages));
            ctx.a = PeoplesD.Best(ctx.map, SkillDefOf.Social) ?? PeoplesD.Able(ctx.map).FirstOrDefault();
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            PeopleEvents.AgeTags(tags, ctx.faction);
            tags.Add(PeopleEventsMath.Means(PeopleEvents.AgeOf(ctx.faction)));
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option <= 1 ? "book" : "thanks");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The crates taken in trust (PeopleTrust.Take): carried in and set down by the stores, the fetch to come. <see cref="free"/>: no fee.</summary>
    public class DilemmaEffect_TrustTake : DilemmaEffect
    {
        public bool free;

        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc == null) return;
            sheet.Play("CC_PEv_Impact_Trust".Translate(hc.count, hc.stranger?.LabelShort ?? "?").Resolve(), "CC_PEv_Impact_TrustBrief".Translate().Resolve());
            sheet.Add(ImpactKind.Goods, free ? 0 : 1, (free ? "CC_PEv_Impact_TrustFree" : "CC_PEv_Impact_TrustFee").Translate(hc.variant).Resolve(), "CC_PEv_Impact_TrustFeeBrief".Translate().Resolve(), "pevfee");
            sheet.Add(ImpactKind.Goodwill, -1, "CC_PEv_Impact_TrustRisk".Translate(hc.faction?.Name ?? "?").Resolve(), "CC_PEv_Impact_TrustRiskBrief".Translate().Resolve(), "pevtrustrisk");
        }

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            if (hc == null) return;
            PeopleTrust.Take(hc, (hc.note ?? "").Split('|').Where(s => s.Contains(":")).ToList(), free);
        }
    }

    // ===================== The terms that follow =====================

    /// <summary>Good ink: a trader of the league (a caravan's or a visitor's) gives the colony a few points better prices for a year.</summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.TradePriceImprovementOffsetForPlayer), MethodType.Getter)]
    public static class Patch_TrustTermsPawn
    {
        public static void Postfix(Pawn __instance, ref float __result)
        {
            if (!RimfolkPatches.On || __instance?.Faction == null || __instance.Faction.IsPlayer) return;
            try
            {
                __result += PeopleTrust.Terms(__instance.Faction);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events: good ink with a trader", e);
            }
        }
    }

    /// <summary>Good ink: a town of the league gives the colony's caravan the same better prices.</summary>
    [HarmonyPatch(typeof(Settlement), nameof(Settlement.TradePriceImprovementOffsetForPlayer), MethodType.Getter)]
    public static class Patch_TrustTermsTown
    {
        public static void Postfix(Settlement __instance, ref float __result)
        {
            if (!RimfolkPatches.On || __instance?.Faction == null) return;
            try
            {
                __result += PeopleTrust.Terms(__instance.Faction);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events: good ink at a town", e);
            }
        }
    }

    /// <summary>Red ink: no league caravan comes for a season.</summary>
    [HarmonyPatch(typeof(IncidentWorker_TraderCaravanArrival), nameof(IncidentWorker_TraderCaravanArrival.FactionCanBeGroupSource))]
    public static class Patch_TrustRedInk
    {
        public static void Postfix(Faction f, ref bool __result)
        {
            if (!__result || !RimfolkPatches.On) return;
            try
            {
                if (PeopleTrust.RedInk(f)) __result = false;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events: red ink's caravans", e);
            }
        }
    }
}
