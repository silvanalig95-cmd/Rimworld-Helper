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
    // ===================== 76. A sleeper of theirs (the Hollow Fleet) =====================

    /// <summary>
    /// A sleeper of theirs (item 76): one of the game's old cryptosleep caskets on the colony's land, found and in reach, holds a sleeper
    /// in the Hollow Fleet's deck grey, one of the ship's lost sleepers, and a deck-runner of the Fleet comes to the gate for them. Handed
    /// over, the runner opens the casket (its group of caskets left shut, so nothing else wakes), the sleeper wakes sick from the long
    /// sleep and walks home with them, and the Fleet pays for them and thinks better of the colony (+12). Kept, one of the colony opens the
    /// casket and the sleeper joins the colony (the Fleet minds a little). Left shut, the sleeper stays asleep and the runner goes; woken
    /// later at the colony, they find their own way home. The sleeper is put in the casket when the runner comes, in place of whoever lay
    /// there (whom nobody had seen). One of the events' visits (kind "pev:sleeper"): x the runner, a the colonist who opens it, group the
    /// sleeper, place the casket, stake 1 when kept, its text "people|pay".
    /// </summary>
    public static class SleeperOfTheirs
    {
        public const string Kind = "pev:sleeper";
        public const string EventDef = "Rimfolk_Evt_FleetSleeperCasket";

        private static int Now => Find.TickManager.TicksGame;

        /// <summary>An old casket on the colony's land with a sleeper in it, found (out of the fog) and in reach from the gate, not already the Fleet's.</summary>
        public static Building_AncientCryptosleepCasket Casket(Map map)
        {
            if (map == null || ThingDefOf.AncientCryptosleepCasket == null) return null;
            var gate = PeoplesD.GateSpot(map);
            var b = PeopleEvents8Book.Instance;
            return map.listerThings.ThingsOfDef(ThingDefOf.AncientCryptosleepCasket).OfType<Building_AncientCryptosleepCasket>()
                .Where(c => c.Spawned && !c.Position.Fogged(map) && c.HasAnyContents && c.ContainedThing is Pawn p && p.RaceProps.Humanlike
                            && (b == null || !b.sleepers.Any(s => !s.done && s.casket == c))
                            && (gate == null || map.reachability.CanReach(gate.Value, c, PathEndMode.Touch, TraverseParms.For(TraverseMode.PassDoors))))
                .OrderBy(c => gate.HasValue ? c.Position.DistanceToSquared(gate.Value) : 0).FirstOrDefault();
        }

        /// <summary>
        /// The Fleet's sleeper put in the casket in place of whoever lay there, the casket set apart from its group (opening it wakes no
        /// other). False when it couldn't be done.
        /// </summary>
        public static bool Lay(Building_AncientCryptosleepCasket casket, Pawn sleeper)
        {
            if (casket == null || sleeper == null || casket.Destroyed) return false;
            try
            {
                var held = casket.GetDirectlyHeldThings();
                var old = held.OfType<Pawn>().ToList();
                foreach (var o in old)
                {
                    held.Remove(o);
                    Arrivals.Discard(o);
                }
                if (!casket.TryAcceptThing(sleeper, false)) return false;
                casket.groupID = Find.UniqueIDsManager.GetNextAncientCryptosleepCasketGroupID();
                return true;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 8: the sleeper laid in the casket", e);
                return false;
            }
        }

        private static Pawn SleeperOf(Visit v) => v?.group.FirstOrDefault();

        /// <summary>The answer taken: the runner (or one of the colony) walks to the casket and opens it; with the play-outs off, at once.</summary>
        public static void Begin(HappeningContext ctx, bool keep)
        {
            var map = ctx?.map;
            var t = CouncilGameComponent.Instance?.happenings;
            var x = ctx?.stranger;
            var casket = ctx?.place;
            if (map == null || t == null || x == null || casket == null || ctx.b == null || ctx.faction == null) return;
            var opener = keep ? PeoplesD.Best(map, SkillDefOf.Medicine) ?? ctx.a : x;
            var v = t.AddVisit(Kind, ctx.incident, map, x, opener, new List<Pawn> { ctx.b }, Now + 10 * GenDate.TicksPerHour);
            v.place = casket;
            v.stake = keep ? 1 : 0;
            v.text = ctx.faction.GetUniqueLoadID() + "|" + ctx.variant;
            if (PlayOuts.On && opener != null && casket.Spawned)
            {
                bool started = opener == x
                    ? PeopleEvents8.Errand(x, map, casket, null, PeopleEvents8.OpenCasketJob, PeopleEvents8Math.OpenTicks, 6f)
                    : PeoplesG.Up(opener, map) && !opener.Drafted && !opener.InMentalState && PeopleEvents8.Send(opener, PeopleEvents8.OpenCasketJob, casket, PeopleEvents8Math.OpenTicks);
                if (started) return;
            }
            Woken(v);
        }

        /// <summary>The visit's hour: the hour at the casket not under way any more (or the hours run out), it is opened there and then.</summary>
        public static bool Hour(Visit v, int now)
        {
            if (v.done) return false;
            var opener = v.a;
            bool working = opener != null && opener.Spawned && opener.CurJobDef == PeopleEvents8.OpenCasketJob;
            if (working && now <= v.until) return true;
            Woken(v);
            return false;
        }

        /// <summary>The hour at the casket done: it opens.</summary>
        public static void Opened(Pawn by, Thing casket)
        {
            var v = CouncilGameComponent.Instance?.happenings?.visits?.FirstOrDefault(x => x != null && x.kind == Kind && !x.done && x.place == casket);
            if (v != null) Woken(v);
        }

        /// <summary>
        /// The casket opened and the sleeper woken, once: handed over, they walk home with the runner and the Fleet pays; kept, they
        /// join the colony. Told.
        /// </summary>
        private static void Woken(Visit v)
        {
            if (v.done) return;
            v.done = true;
            var x = v.x;
            var map = v.map;
            var sleeper = SleeperOf(v);
            var casket = v.place as Building_Casket;
            var f = x?.Faction ?? PeopleEvents8.PeopleOf(v);
            bool keep = v.stake == 1;
            try
            {
                if (casket != null && !casket.Destroyed && casket.Spawned && casket.HasAnyContents) casket.EjectContents();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 8: the casket opened", e);
            }
            if (sleeper == null || sleeper.Dead || !sleeper.Spawned)
            {
                if (x != null) Arrivals.SendAway(new List<Pawn> { x });
                return;
            }
            if (keep)
            {
                Arrivals.Join(sleeper, map);
                PeoplesG.SayLine(sleeper, v.incident, "stay");
                Tell.Message(NewsKind.People, Importance.Notable, map, "CC_PEv8_SleeperStays".Translate(sleeper.Named("PAWN"), (f?.Name ?? "?").Named("FACTION")).Resolve(),
                    new LookTargets(sleeper), MessageTypeDefOf.PositiveEvent);
                if (f != null && f.HasGoodwill && !f.IsPlayer && !f.defeated) WorldSituationWorker.Goodwill(f, PeopleEvents8Math.KeptGoodwill, GoodwillWhy.KeptTheirs);
                if (x != null) Arrivals.SendAway(new List<Pawn> { x });
                return;
            }
            var parts = (v.text ?? "").Split('|');
            int pay = parts.Length > 1 && int.TryParse(parts[1], out int n) ? n : 0;
            PeoplesG.SayLine(x, v.incident, "home");
            if (pay > 0 && map != null && f != null) Couriers.PaySilver(map, PeoplesD.GateSpot(map) ?? sleeper.Position, pay, f, x);
            Tell.Message(NewsKind.People, Importance.Notable, map, "CC_PEv8_SleeperHome".Translate(sleeper.Named("PAWN"), (x?.LabelShort ?? "?").Named("RUNNER"), (f?.Name ?? "?").Named("FACTION"),
                pay.Named("COUNT")).Resolve(), new LookTargets(sleeper), MessageTypeDefOf.PositiveEvent);
            PeopleEvents8.Goodwill(f, PeopleEvents8Math.SleeperGoodwill, GoodwillWhy.Returned);
            var all = new List<Pawn> { sleeper };
            if (x != null && x.Spawned && x.Map == map) all.Add(x);
            try
            {
                Lords.Leave(all);
                if (f != null && !f.HostileTo(Faction.OfPlayer)) LordMaker.MakeNewLord(f, new LordJob_ExitMapBest(Pace.Errand, false, true), map, all);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 8: the sleeper walks home", e);
            }
        }

        /// <summary>Left shut: the sleeper kept on record, so that woken later at the colony they find their own way home.</summary>
        public static void LeftShut(HappeningContext ctx)
        {
            if (ctx?.b == null || ctx.place == null) return;
            PeopleEvents8Book.Instance?.AddSleeper(new SleeperRecord { casket = ctx.place, sleeper = ctx.b, faction = ctx.faction, map = ctx.map });
        }

        /// <summary>The events' hour: a sleeper of theirs left asleep and woken later at the colony walks off to find the Fleet; told.</summary>
        public static void Watch(int now)
        {
            var b = PeopleEvents8Book.Instance;
            if (b == null) return;
            foreach (var r in b.sleepers.Where(r => r != null && !r.done).ToList())
            {
                var p = r.sleeper;
                if (p == null || p.Dead || p.Discarded || p.Faction == Faction.OfPlayer || p.IsPrisoner || r.casket == null || r.casket.Destroyed && !p.Spawned)
                {
                    r.done = true;
                    continue;
                }
                if (!p.Spawned || p.Downed || p.GetLord() != null) continue;
                r.done = true;
                Tell.Message(NewsKind.People, Importance.Notable, p.Map, "CC_PEv8_SleeperWalksHome".Translate(p.Named("PAWN"), (r.faction?.Name ?? "?").Named("FACTION")).Resolve(),
                    new LookTargets(p), MessageTypeDefOf.NeutralEvent);
                Arrivals.SendAway(new List<Pawn> { p });
            }
        }

        /// <summary>Dev mode: the deck-runner at the gate for a sleeper in the nearest old casket now.</summary>
        public static string Now_(Map map)
        {
            var f = PeoplesD.Friendly(PeoplesMath.Crew);
            if (map == null || f == null) return "no Hollow Fleet at peace.";
            if (Casket(map) == null) return "no old casket with a sleeper here that the colony has found (spawn an ancient cryptosleep casket with a pawn).";
            return PeopleEvents2.Fire(EventDef, map, f) ? "a deck-runner of " + f.Name + " comes for a sleeper." : "the runner could not come now (a question waits, or a threat is on).";
        }
    }

    /// <summary>
    /// A deck-runner of {FACTION} ({X}) at the gate: one of the old caskets on the colony's land holds {B}, a sleeper of the hull, asleep
    /// {COUNT} years; the Fleet would take them home and pay {RELATION} silver.
    /// </summary>
    public class HappeningWorker_FleetSleeperCasket : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) => PeoplesD.Friendly(PeoplesMath.Crew) != null && SleeperOfTheirs.Casket(map) != null && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Crew);
            var casket = SleeperOfTheirs.Casket(ctx.map);
            if (ctx.faction == null || casket == null) return false;
            ctx.place = casket;
            ctx.b = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_HollowFleet_Deckhand"), ctx.faction, new FloatRange(19f, 45f));
            if (ctx.b == null) return false;
            ctx.count = PeoplesFMath.SleptYears(Rand.Value);
            try
            {
                ctx.b.ageTracker.AgeChronologicalTicks += (long)ctx.count * GenDate.TicksPerYear;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 8: the sleeper's long sleep", e);
            }
            ctx.variant = PeopleEvents8Math.SleeperPay(LivingEvents.WealthFactor(ctx.map));
            ctx.relation = ctx.variant.ToString();
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_HollowFleet_Deckhand"), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            ctx.a = PeoplesD.Best(ctx.map, SkillDefOf.Medicine) ?? PeoplesD.Able(ctx.map).FirstOrDefault();
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            if (Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) == null) return false;
            // The sleeper the Fleet's records say is there: theirs, in the casket, from now on.
            if (SleeperOfTheirs.Lay(ctx.place as Building_AncientCryptosleepCasket, ctx.b)) return true;
            Arrivals.SendAway(PeoplesG.Party(ctx));
            return false;
        }

        protected override void Abort(HappeningContext ctx)
        {
            if (ctx.b != null && !ctx.b.Spawned && ctx.b.ParentHolder == null) Arrivals.Discard(ctx.b);
            base.Abort(ctx);
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags) => PeopleEvents.AgeTags(tags, ctx.faction);

        public override string Unavailable(HappeningContext ctx, int option)
        {
            if (option > 1) return null;
            var c = ctx.place as Building_Casket;
            return c == null || c.Destroyed || !c.Spawned || !c.HasAnyContents || c.ContainedThing != ctx.b ? "CC_PEv8_CasketOpened".Translate().Resolve() : null;
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option == 0 ? "handover" : option == 1 ? "kept" : "later");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The casket opened and the sleeper woken (SleeperOfTheirs.Begin): handed over to the runner, or with <see cref="keep"/> kept at the colony.</summary>
    public class DilemmaEffect_FleetSleeper : DilemmaEffect
    {
        public bool keep;

        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc?.b == null || hc.stranger == null) return;
            if (keep)
            {
                var opener = PeoplesD.Best(hc.map, SkillDefOf.Medicine) ?? hc.a;
                sheet.Play("CC_PEv8_Impact_OpenKeep".Translate(opener?.LabelShort ?? "?").Resolve(), "CC_PEv8_Impact_OpenBrief".Translate().Resolve());
                sheet.Add(ImpactKind.People, 1, "CC_Impact_Join".Translate(hc.b.LabelShort).Resolve(), "CC_Impact_JoinBrief".Translate(hc.b.LabelShort).Resolve(), "pev8sleeper");
                if (hc.faction != null) sheet.Goodwill(hc.faction, PeopleEvents8Math.KeptGoodwill);
                return;
            }
            sheet.Play("CC_PEv8_Impact_OpenHand".Translate(hc.stranger.LabelShort, hc.b.LabelShort).Resolve(), "CC_PEv8_Impact_OpenBrief".Translate().Resolve());
            sheet.Silver(hc.variant);
            if (hc.faction != null) sheet.Goodwill(hc.faction, PeopleEvents8Math.SleeperGoodwill);
        }

        public override void Apply(DilemmaContext ctx) => SleeperOfTheirs.Begin(ctx as HappeningContext, keep);
    }

    /// <summary>The casket left shut: the sleeper kept on record, to walk home if woken later.</summary>
    public class DilemmaEffect_FleetSleeperShut : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
        }

        public override void Apply(DilemmaContext ctx) => SleeperOfTheirs.LeftShut(ctx as HappeningContext);
    }
}
