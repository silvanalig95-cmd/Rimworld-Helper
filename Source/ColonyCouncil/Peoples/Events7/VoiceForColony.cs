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
    // ===================== 64. A voice for the colony (the Signal Line) =====================

    /// <summary>
    /// A voice for the colony (item 64): once a season an operator of the Signal Line comes to the gate with one call on the Line's
    /// masts for the colony, at the Line's price: a trader of goods or a trader of arms (the game's own caravan of that kind, from a
    /// people at peace that sends one), hands (the game's own wanderer who comes to join), or a good word to a people the colony
    /// stands poorly with (a little goodwill). Where the colony has a radio (a powered comms console, or its own relay mast singing)
    /// the operator walks to it and makes the call there, and it is answered within hours; where it has none, a runner of the Line
    /// carries the call off on foot, and it is answered in a day or two. One of the events' visits (kind "pev:voice"): x the operator
    /// or runner, place the radio, text "call|traderKind|people", stake 1 by radio, due when it is answered (-1 until called).
    /// </summary>
    public static class VoiceForColony
    {
        public const string Kind = "pev:voice";
        public const string EventDef = "Rimfolk_Evt_VoiceForColony";

        private static int Now => Find.TickManager.TicksGame;

        /// <summary>The colony's radio: a powered comms console, else its own relay mast while it sings; null when it has neither.</summary>
        public static Thing Radio(Map map)
        {
            if (map == null) return null;
            var console = map.listerBuildings.allBuildingsColonist.OfType<Building_CommsConsole>().FirstOrDefault(c => c.Spawned && c.CanUseCommsNow);
            if (console != null) return console;
            return RelayMast.Working(map) ? RelayMast.Of(map) : null;
        }

        /// <summary>A people at peace that sends a caravan of this kind of goods ("goods", "arms"), and the kind: the nearest and friendliest first.</summary>
        public static Faction Trader(Map map, string call, out TraderKindDef kind)
        {
            kind = null;
            var tech = Avail.EffectiveTech;
            foreach (var f in Find.FactionManager.AllFactionsListForReading.Where(f => Arrivals.Usable(f) && !f.def.caravanTraderKinds.NullOrEmpty() && !UnionStrike.Striking(f))
                         .OrderByDescending(f => Arrivals.TechWeight(f, tech) * (f.PlayerGoodwill + 120)))
            {
                var k = f.def.caravanTraderKinds.FirstOrDefault(t => t != null && PeopleEvents7Math.TradeKind(t.defName) == call);
                if (k == null) continue;
                kind = k;
                return f;
            }
            return null;
        }

        /// <summary>The people the colony stands most poorly with that are not at war with it (a good word helps there most).</summary>
        public static Faction WordFor(Faction line) =>
            Find.FactionManager.AllFactionsListForReading.Where(f => Arrivals.Usable(f) && f != line && f.HasGoodwill && f.def.humanlikeFaction && f.PlayerGoodwill < 75)
                .OrderBy(f => f.PlayerGoodwill).FirstOrDefault();

        private static Visit Of(Pawn x) => CouncilGameComponent.Instance?.happenings?.visits?.FirstOrDefault(v => v != null && v.kind == Kind && !v.done && v.x == x);

        private static string[] Parts(Visit v) => (v?.text ?? "").Split('|');

        /// <summary>
        /// The call taken: by radio, the operator walks to it and calls (an hour, sparks at it), and it is answered within hours; by
        /// runner, the runner sets off with it, and it is answered in a day or two. With the play-outs off, called at once.
        /// </summary>
        public static void Begin(HappeningContext ctx, string call)
        {
            var map = ctx?.map;
            var t = CouncilGameComponent.Instance?.happenings;
            var x = ctx?.stranger;
            if (map == null || t == null || x == null || call.NullOrEmpty()) return;
            TraderKindDef kind = null;
            Faction to = call == PeopleEvents7Math.CallWord ? ctx.enemy : null;
            if (call == PeopleEvents7Math.CallGoods || call == PeopleEvents7Math.CallArms) to = Trader(map, call, out kind);
            var radio = ctx.place != null && ctx.place.Spawned && ctx.place.Map == map ? ctx.place : null;
            var v = t.AddVisit(Kind, ctx.incident, map, x, ctx.a, null, Now + 4 * GenDate.TicksPerDay);
            v.place = radio;
            v.stake = radio != null ? 1 : 0;
            v.text = call + "|" + (kind?.defName ?? "") + "|" + (to?.GetUniqueLoadID() ?? "");
            v.due = -1;
            if (radio != null && PlayOuts.On && PeoplesE.Up(x, map) && x.CanReach(radio, PathEndMode.Touch, Danger.Some))
            {
                Lords.Leave(x);
                LordMaker.MakeNewLord(x.Faction, new LordJob_VisitOnErrand(x.Faction, radio.Position, 3 * GenDate.TicksPerHour), map, new List<Pawn> { x });
                if (PeopleEvents7.Send(x, PeopleEvents7.CallJob, radio, PeopleEvents7Math.CallTicks)) return;
            }
            Sent(v);
        }

        /// <summary>The call made (the hour at the radio done, or the runner on the road): when it is answered.</summary>
        private static void Sent(Visit v)
        {
            if (v.due >= 0) return;
            bool radio = v.stake == 1;
            v.due = Now + PeopleEvents7Math.CallHours(radio, Rand.Value) * GenDate.TicksPerHour;
            if (v.x != null && v.x.Spawned)
            {
                PeoplesG.SayLine(v.x, v.incident, radio ? "called" : "off");
                Arrivals.SendAway(new List<Pawn> { v.x });
            }
            Tell.Message(NewsKind.World, Importance.Notable, v.map, ("CC_PEv7_Call_" + (radio ? "Radio_" : "Runner_") + Parts(v)[0]).Translate((v.x?.LabelShort ?? "?").Named("PAWN")).Resolve(),
                v.place != null && v.place.Spawned ? new LookTargets(v.place) : LookTargets.Invalid, MessageTypeDefOf.NeutralEvent);
        }

        /// <summary>The operator's hour at the radio done.</summary>
        public static void Called(Pawn x)
        {
            var v = Of(x);
            if (v != null) Sent(v);
        }

        /// <summary>The visit's hour: the call made if the operator was kept from it; when it is answered, what comes.</summary>
        public static bool Hour(Visit v, int now)
        {
            if (v.done) return false;
            if (v.due < 0)
            {
                if (v.x != null && v.x.Spawned && v.x.CurJobDef == PeopleEvents7.CallJob && now < v.until) return true;
                Sent(v);
                return true;
            }
            if (now < v.due) return true;
            if (v.map == null || !Find.Maps.Contains(v.map))
            {
                v.done = true;
                return false;
            }
            if (MapFacts.ThreatOn(v.map) && now < v.until)
            {
                v.due = now + GenDate.TicksPerHour * 2;
                return true;
            }
            v.done = true;
            Answer(v);
            return false;
        }

        /// <summary>The call answered: the caravan of that kind, the wanderer who joins, or the good word said; told.</summary>
        private static void Answer(Visit v)
        {
            var p = Parts(v);
            string call = p[0];
            var map = v.map;
            var people = p.Length > 2 && p[2].Length > 0 ? Find.FactionManager.AllFactionsListForReading.FirstOrDefault(f => f.GetUniqueLoadID() == p[2]) : null;
            try
            {
                switch (call)
                {
                    case PeopleEvents7Math.CallGoods:
                    case PeopleEvents7Math.CallArms:
                    {
                        var kind = p.Length > 1 && p[1].Length > 0 ? DefDatabase<TraderKindDef>.GetNamedSilentFail(p[1]) : null;
                        if (people == null || kind == null || !Arrivals.Usable(people)) people = Trader(map, call, out kind);
                        if (people != null && kind != null && Caravan(map, people, kind)) return;
                        break;
                    }
                    case PeopleEvents7Math.CallHands:
                    {
                        var inc = IncidentDefOf.WandererJoin;
                        var parms = StorytellerUtility.DefaultParmsNow(inc.category, map);
                        parms.forced = true;
                        if (inc.Worker.CanFireNow(parms) && inc.Worker.TryExecute(parms)) return;
                        break;
                    }
                    case PeopleEvents7Math.CallWord:
                        if (people != null && Arrivals.Usable(people))
                        {
                            WorldSituationWorker.Goodwill(people, PeopleEvents7Math.GoodWord, GoodwillWhy.Name);
                            Tell.Message(NewsKind.World, Importance.Notable, map, "CC_PEv7_Call_WordSaid".Translate(people.Name.Named("FACTION")).Resolve(), LookTargets.Invalid,
                                MessageTypeDefOf.PositiveEvent);
                            return;
                        }
                        break;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 7: the call answered", e);
            }
            // Unanswered: the Line sends the colony's silver back by its courier.
            int fee = call == PeopleEvents7Math.CallHands ? PeopleEvents7Math.HandsFee : PeopleEvents7Math.CallFee;
            var line = v.x?.Faction ?? PeoplesD.Friendly(PeoplesMath.Linesmen);
            if (map != null) Couriers.PaySilver(map, PeoplesD.GateSpot(map) ?? CampSite.ColonyCenter(map), fee, line);
            Tell.Message(NewsKind.World, Importance.Notable, map, "CC_PEv7_Call_Unanswered".Translate(fee.Named("COUNT")).Resolve(), LookTargets.Invalid, MessageTypeDefOf.NeutralEvent);
        }

        /// <summary>The game's own trader caravan of that kind, from that people.</summary>
        private static bool Caravan(Map map, Faction f, TraderKindDef kind)
        {
            var inc = IncidentDefOf.TraderCaravanArrival;
            if (inc?.Worker == null) return false;
            var parms = StorytellerUtility.DefaultParmsNow(inc.category, map);
            parms.faction = f;
            parms.traderKind = kind;
            parms.forced = true;
            return inc.Worker.CanFireNow(parms) && inc.Worker.TryExecute(parms);
        }
    }

    /// <summary>
    /// An operator of {FACTION} ({X}) at the gate with one call a season on the Line's masts for the colony: a trader of goods or of arms,
    /// hands, or a good word to {ENEMY}; by the colony's radio ({TALE}), or carried by a runner where it has none.
    /// </summary>
    public class HappeningWorker_VoiceForColony : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) => PeoplesD.Friendly(PeoplesMath.Linesmen) != null && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Linesmen);
            if (ctx.faction == null) return false;
            ctx.place = VoiceForColony.Radio(ctx.map);
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, ctx.place != null ? "Rimfolk_SignalLine_Operator" : "Rimfolk_SignalLine_Lineman"), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            ctx.tale = ctx.place == null ? "" : ctx.place.def == PeoplesF.MastDef ? "CC_PEv7_RadioMast".Translate().Resolve() : "CC_PEv7_RadioConsole".Translate().Resolve();
            ctx.count = PeopleEvents7Math.CallFee;
            ctx.dead = PeopleEvents7Math.HandsFee.ToString();
            ctx.enemy = VoiceForColony.WordFor(ctx.faction);
            ctx.a = KinMemory.Greeter(ctx.faction, ctx.map) ?? PeoplesD.Able(ctx.map).FirstOrDefault();
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            PeopleEvents.AgeTags(tags, ctx.faction);
            tags.Add(ctx.place != null ? "radio" : "runner");
            if (ctx.enemy != null) tags.Add("word");
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option <= 3 ? "call" : "later");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The call made for the colony (VoiceForColony.Begin): <see cref="call"/> "goods", "arms", "hands" or "word".</summary>
    public class DilemmaEffect_VoiceCall : DilemmaEffect
    {
        public string call;

        public override bool ChanceEach => false;

        public override string Unavailable(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            if (hc?.map == null) return null;
            switch (call)
            {
                case PeopleEvents7Math.CallGoods:
                case PeopleEvents7Math.CallArms:
                    return VoiceForColony.Trader(hc.map, call, out _) == null ? "CC_PEv7_Call_NoTrader".Translate().Resolve() : null;
                case PeopleEvents7Math.CallWord:
                    return hc.enemy == null ? "CC_PEv7_Call_NoWord".Translate().Resolve() : null;
            }
            return null;
        }

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc?.map == null) return;
            bool radio = hc.place != null;
            switch (call)
            {
                case PeopleEvents7Math.CallGoods:
                case PeopleEvents7Math.CallArms:
                {
                    var f = VoiceForColony.Trader(hc.map, call, out var kind);
                    if (f == null || kind == null) return;
                    sheet.Play((radio ? "CC_PEv7_Impact_CallTraderRadio" : "CC_PEv7_Impact_CallTraderRunner").Translate(kind.label, f.Name).Resolve(), "CC_PEv7_Impact_CallTraderBrief".Translate().Resolve());
                    break;
                }
                case PeopleEvents7Math.CallHands:
                    sheet.Play((radio ? "CC_PEv7_Impact_CallHandsRadio" : "CC_PEv7_Impact_CallHandsRunner").Translate().Resolve(), "CC_PEv7_Impact_CallHandsBrief".Translate().Resolve());
                    break;
                case PeopleEvents7Math.CallWord:
                    if (hc.enemy == null) return;
                    sheet.Play((radio ? "CC_PEv7_Impact_CallWordRadio" : "CC_PEv7_Impact_CallWordRunner").Translate(hc.enemy.Name).Resolve(), "CC_PEv7_Impact_CallWordBrief".Translate().Resolve());
                    sheet.Goodwill(hc.enemy, PeopleEvents7Math.GoodWord);
                    break;
            }
        }

        public override void Apply(DilemmaContext ctx) => VoiceForColony.Begin(ctx as HappeningContext, call);
    }
}
