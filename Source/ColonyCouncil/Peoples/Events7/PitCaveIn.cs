using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // ===================== 67. The pit's cave-in (the Deepwell Union) =====================

    /// <summary>
    /// The pit's cave-in (item 67): one of the Deepwell Union's pits within reach has caved in with miners below, and a runner comes to
    /// ask for the colony's best miner and its doctor. Sent, each sets out as a caravan of one (round 2's season away: the game's own
    /// roads there and back) for the pit, where they dig for four to six days, a shift a day: the miner at the face, the doctor with the
    /// hurt, each learning. One shift in twenty brings a crush for one of them (the drill rig's own chance and harm kind,
    /// HarmKinds.UnionDrill, in the shared budget; at most once each), told as word from the pit when it happens, the wound bound there
    /// by the colony's doctor if they are at the pit, else by the union's. Home again they bring the union's pay, a good memory of the
    /// trapped dug out, and the union thinks better of the colony (twelve sent both, six alone). One season away a colonist (kind
    /// "pev:away", key "cavein"); its shifts are counted by the days since they got there, the crushed kept in the visit's touched.
    /// </summary>
    public static class PitCaveIn
    {
        public const string EventDef = "Rimfolk_Evt_PitCaveIn";
        public const string AwayKey = "cavein";

        private static int Now => Find.TickManager.TicksGame;

        /// <summary>The union's pit within reach of the home: its nearest town, within so many tiles.</summary>
        public static Settlement Pit(Faction f, Map map)
        {
            var town = f != null && map != null ? WorldSituationWorker.NearestTown(f, map) : null;
            return town != null && Find.WorldGrid.ApproxDistanceInTiles(map.Tile, town.Tile) <= PeopleEvents7Math.CaveInTiles ? town : null;
        }

        /// <summary>The colony's best miner who could go, and its best doctor besides.</summary>
        public static Pawn Miner(Map map) => PeoplesD.Best(map, SkillDefOf.Mining, p => PeopleEvents2.MayLeave(p, map) && !p.WorkTypeIsDisabled(WorkTypeDefOf.Mining));

        public static Pawn Doctor(Map map, Pawn not) =>
            PeoplesD.Best(map, SkillDefOf.Medicine, p => p != not && PeopleEvents2.MayLeave(p, map) && !p.WorkTypeIsDisabled(WorkTypeDefOf.Doctor));

        /// <summary>Both may go now: never the colony's last two grown-ups (MayLeave), and not both of its last three.</summary>
        public static bool BothMayGo(Pawn a, Pawn b, Map map) =>
            a != null && b != null && PeopleEvents2.MayLeave(a, map) && PeopleEvents2.MayLeave(b, map) && PeoplesD.Able(map).Count(x => !x.DevelopmentalStage.Juvenile()) > 3;

        /// <summary>The answer taken: the miner (and the doctor) set out for the pit, each a caravan of one, for the same days.</summary>
        public static void Send(HappeningContext ctx, bool both)
        {
            var map = ctx?.map;
            var pit = Pit(ctx?.faction, map);
            if (map == null || pit == null || ctx.a == null) return;
            int days = ctx.variant > 0 ? ctx.variant : PeopleEvents7Math.CaveInDays(Rand.Value);
            var inc = ctx.incident ?? DefDatabase<IncidentDef>.GetNamedSilentFail(EventDef);
            var went = new List<Pawn>();
            if (SeasonAway.Send(AwayKey, inc, ctx.a, null, ctx.faction, pit.Tile, pit.Label, days, map, both ? "both" : "alone") != null) went.Add(ctx.a);
            if (both && ctx.b != null && SeasonAway.Send(AwayKey, inc, ctx.b, null, ctx.faction, pit.Tile, pit.Label, days, map, "both") != null) went.Add(ctx.b);
            if (went.Count > 0) Arrivals.SendAway(PeoplesG.Party(ctx));
        }

        /// <summary>The seasons at the pit under way now.</summary>
        private static IEnumerable<Visit> There() =>
            CouncilGameComponent.Instance?.happenings?.visits?.Where(v => v != null && v.kind == SeasonAway.Kind && !v.done && SeasonAway.KeyOf(v) == AwayKey && v.stake == PeopleEvents2Math.There)
            ?? Enumerable.Empty<Visit>();

        /// <summary>
        /// The events' hour: for each colonist at the pit, a shift ends each day they are there: what they learn, and one shift in twenty
        /// a crush (at most once each), told as word from the pit.
        /// </summary>
        public static void Shifts(int now)
        {
            foreach (var v in There().ToList())
            {
                var p = v.x;
                if (p == null || p.Dead || v.due < 0) continue;
                int arrived = v.due - Mathf.RoundToInt(SeasonAway.DaysOf(v) * GenDate.TicksPerDay);
                int since = now - arrived;
                if (since < GenDate.TicksPerDay || since % GenDate.TicksPerDay >= GenDate.TicksPerHour) continue;
                int shift = since / GenDate.TicksPerDay;
                bool doctor = (p.skills?.GetSkill(SkillDefOf.Medicine)?.Level ?? 0) > (p.skills?.GetSkill(SkillDefOf.Mining)?.Level ?? 0);
                try
                {
                    p.skills?.Learn(doctor ? SkillDefOf.Medicine : SkillDefOf.Mining, PeopleEvents7Math.ShiftXp, true);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("peoples' events 7: a shift at the pit", e);
                }
                if (v.touched.Contains(p) || !Harm.On || !Harm.Roll(p, HarmKinds.UnionDrill, PeopleEvents7Math.CrushChance)) continue;
                Crush(v, p, shift);
            }
        }

        /// <summary>A fall of rock on a shift: a crushed foot or hand, bound there (by the colony's doctor if they are at the pit), told.</summary>
        private static void Crush(Visit v, Pawn p, int shift)
        {
            var wound = Harm.Injure(p, HurtKind.Crush, Rand.Bool ? HurtWhere.Foot : HurtWhere.Hand, PeopleEvents7Math.CrushLow, PeopleEvents7Math.CrushHigh, HarmKinds.UnionDrill);
            if (wound == null) return;
            v.touched.Add(p);
            var doctor = There().Select(x => x.x).FirstOrDefault(x => x != null && x != p && !x.Dead);
            try
            {
                float q = doctor != null ? Mathf.Clamp01(0.3f + (doctor.skills?.GetSkill(SkillDefOf.Medicine)?.Level ?? 0) * 0.04f) : 0.5f;
                wound.Tended(q, 1f);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 7: the crush bound", e);
            }
            var f = PeoplesF.PeopleOf(v);
            Tell.Message(NewsKind.People, Importance.Important, v.map,
                (doctor != null ? "CC_PEv7_PitCrushDoctor" : "CC_PEv7_PitCrush").Translate(p.Named("PAWN"), (wound.Part?.Label ?? "").Named("PART"), SeasonAway.PlaceOf(v).Named("PLACE"),
                    Find.ActiveLanguageWorker.OrdinalNumber(shift).Named("SHIFT"), (doctor?.LabelShort ?? f?.Name ?? "?").Named("DOCTOR")).Resolve(), LookTargets.Invalid,
                MessageTypeDefOf.NegativeHealthEvent);
        }

        /// <summary>Dev mode: a shift at the pit ends now for everyone there (its crush at its chance).</summary>
        public static string ShiftNow()
        {
            var list = There().ToList();
            if (list.Count == 0) return "nobody of the colony is at a pit now.";
            foreach (var v in list)
            {
                if (v.x == null || v.x.Dead) continue;
                v.x.skills?.Learn(SkillDefOf.Mining, PeopleEvents7Math.ShiftXp, true);
                if (!v.touched.Contains(v.x) && Harm.On && Harm.Roll(v.x, HarmKinds.UnionDrill, 1f)) Crush(v, v.x, 1);
            }
            return "a shift ended at the pit (a crush forced through the budget's own checks).";
        }
    }

    /// <summary>A season at the pit: the letter as they get there, and the pay, the memory and the union's thanks at home.</summary>
    public class CaveInAway : AwayKind
    {
        public override string Key => PitCaveIn.AwayKey;

        public override void Arrived(Visit v, Map home, Faction f, string place)
        {
            var p = v.x;
            Tell.Message(NewsKind.World, Importance.Notable, home, "CC_PEv7_PitThere".Translate(p.Named("PAWN"), place.Named("PLACE"), (f?.Name ?? "?").Named("FACTION")).Resolve(),
                LookTargets.Invalid, MessageTypeDefOf.NeutralEvent);
        }

        public override void Home(Visit v, Map home, Faction f, bool served)
        {
            var p = v.x;
            int pay = PeopleEvents7Math.CaveInPay(Mathf.RoundToInt(SeasonAway.DaysOf(v)));
            if (served)
            {
                if (p.Spawned)
                    foreach (var t in Couriers.Make(ThingDefOf.Silver, pay))
                        GenPlace.TryPlaceThing(t, p.Position, p.Map, ThingPlaceMode.Near);
                PeopleEvents7.Thought(p, "Rimfolk_Thought_DugThemOut");
                bool both = SeasonAway.ExtraOf(v) == "both";
                // Twelve for the two of them sent, six for one alone: half each as each comes home.
                PeopleEvents7.Goodwill(f, both ? PeopleEvents7Math.CaveInGoodwill / 2 : PeopleEvents7Math.CaveInAloneGoodwill, GoodwillWhy.Helped);
            }
            Tell.Message(NewsKind.World, Importance.Notable, home,
                (served ? "CC_PEv7_PitHome" : "CC_PEv7_PitHomeEarly").Translate(p.Named("PAWN"), SeasonAway.PlaceOf(v).Named("PLACE"), (f?.Name ?? "?").Named("FACTION"), pay.Named("PAY")).Resolve(),
                p.Spawned ? new LookTargets(p) : LookTargets.Invalid, MessageTypeDefOf.PositiveEvent);
        }
    }

    /// <summary>
    /// A runner of {FACTION} ({X}) at the gate: the pit at {PLACE} has caved in with miners below; they ask for {A}, the colony's best
    /// miner, and {B}, its doctor, for {COUNT} days.
    /// </summary>
    public class HappeningWorker_PitCaveIn : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext)
        {
            var f = PeoplesD.Friendly(PeoplesMath.Drillers);
            return f != null && PitCaveIn.Pit(f, map) != null && PitCaveIn.Miner(map) != null && Colonists(map).Count > 0;
        }

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Drillers);
            var pit = PitCaveIn.Pit(ctx.faction, ctx.map);
            if (ctx.faction == null || pit == null) return false;
            ctx.a = PitCaveIn.Miner(ctx.map);
            if (ctx.a == null) return false;
            ctx.b = PitCaveIn.Doctor(ctx.map, ctx.a);
            ctx.placeName = pit.Label;
            ctx.variant = PeopleEvents7Math.CaveInDays(Rand.Value);
            ctx.count = ctx.variant;
            ctx.dead = PeopleEvents7Math.CaveInPay(ctx.variant).ToString();
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Deepwell_Miner"), ctx.faction, ext.ages);
            return ctx.stranger != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            PeopleEvents.AgeTags(tags, ctx.faction);
            if (ctx.b != null) tags.Add("doctor");
        }

        public override string Unavailable(HappeningContext ctx, int option)
        {
            if (option == 0 && !PitCaveIn.BothMayGo(ctx.a, ctx.b, ctx.map)) return "CC_PEv7_PitBothCantGo".Translate().Resolve();
            if (option == 1 && !PeopleEvents2.MayLeave(ctx.a, ctx.map)) return "CC_PEv7_PitCantGo".Translate(ctx.a?.LabelShort ?? "?").Resolve();
            return null;
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option <= 1 ? "sent" : "later");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The miner (and with <see cref="both"/>, the doctor) sent to the pit for the days the card says (PitCaveIn.Send).</summary>
    public class DilemmaEffect_PitCaveIn : DilemmaEffect
    {
        public bool both;

        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc?.a == null) return;
            string who = both && hc.b != null ? "CC_PEv7_AAndB".Translate(hc.a.LabelShort, hc.b.LabelShort).Resolve() : hc.a.LabelShort;
            sheet.Play("CC_PEv7_Impact_Pit".Translate(who, hc.placeName ?? "?", hc.variant).Resolve(), "CC_PEv7_Impact_PitBrief".Translate().Resolve());
            sheet.Add(ImpactKind.Goods, 1, "CC_PEv7_Impact_PitPay".Translate(PeopleEvents7Math.CaveInPay(hc.variant)).Resolve(), "CC_PEv7_Impact_PitPayBrief".Translate().Resolve(), "pev7pitpay");
            if (Harm.On)
                sheet.Add(ImpactKind.Health, -1, "CC_PEv7_Impact_PitRisk".Translate(who).Resolve(), "CC_PEv7_Impact_PitRiskBrief".Translate().Resolve(), "pev7pitrisk");
            if (hc.faction != null) sheet.Goodwill(hc.faction, both && hc.b != null ? PeopleEvents7Math.CaveInGoodwill : PeopleEvents7Math.CaveInAloneGoodwill);
        }

        public override void Apply(DilemmaContext ctx) => PitCaveIn.Send(ctx as HappeningContext, both && ctx?.b != null);
    }
}
