using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    // ===================== 89. A mind that shouts (the Choir of the Spire) =====================

    /// <summary>
    /// A mind that shouts (item 89): when a colonist has been three hours in a mental break that turns on nobody (wandering, hiding, a
    /// binge: the game's own), a listener of the Choir may come to the gate, having heard it from the road (once a break, at a chance, not
    /// soon after the last; the code-fired event Rimfolk_Evt_MindShouts). Let in, the listener sits beside them for two hours, following
    /// them about if they wander (round 9's sitting with), and the break ends there, calmly (the game's own recovery and a calm of the
    /// Choir's). Or a colonist of the colony sits with them too and learns how (Social). Or the break runs its course. One of the events'
    /// visits (kind "pev:shout"): x the listener, a the colonist, its text "people|helperId".
    /// </summary>
    public static class MindShouts
    {
        public const string Kind = "pev:shout";
        public const string EventDef = "Rimfolk_Evt_MindShouts";

        private static int Now => Find.TickManager.TicksGame;

        /// <summary>The colonist the event is fired for, while it is.</summary>
        private static Pawn firing;

        /// <summary>A break the Choir could hear: one that turns on nobody, three hours long already.</summary>
        public static bool InLongBreak(Pawn p)
        {
            var ms = p?.MentalState;
            return ms?.def != null && !p.Dead && p.Spawned && PeopleEvents9Math.LongBreak(ms.Age, ms.def.IsAggro) && ms.def != MentalStateDefOf.PanicFlee;
        }

        public static Pawn For(Map map) =>
            firing != null && firing.Map == map && InLongBreak(firing) ? firing : IncidentWorker_Happening.Colonists(map).FirstOrDefault(p => InLongBreak(p) && OrderOn(p) == null);

        public static Visit OrderOn(Pawn p) => CouncilGameComponent.Instance?.happenings?.visits?.FirstOrDefault(v => v != null && v.kind == Kind && !v.done && v.a == p);

        /// <summary>The hour at a home: a long break not rolled for is rolled once; heard, the listener comes.</summary>
        public static void Watch(Map map, int now)
        {
            var b = PeopleEvents9Book.Instance;
            if (b == null) return;
            foreach (var p in IncidentWorker_Happening.Colonists(map).Where(InLongBreak).ToList())
            {
                string key = p.thingIDNumber + ":" + (now - p.MentalState.Age) / GenDate.TicksPerHour;
                if (b.ShoutRolled(key)) continue;
                b.NoteShout(key);
                var f = PeoplesD.Friendly(PeoplesMath.Listeners);
                if (f == null || OrderOn(p) != null || !Tell.CanAsk || MapFacts.ThreatOn(map)) continue;
                if (b.lastShout >= 0 && now - b.lastShout < PeopleEvents9Math.ShoutGapDays * GenDate.TicksPerDay) continue;
                if (!Rand.Chance(PeopleEvents9Math.ShoutChance * StoryDirection.Factor(StoryWaysMath.Visitors))) continue;
                firing = p;
                try
                {
                    if (PeopleEvents2.Fire(EventDef, map, f)) b.lastShout = now;
                }
                finally
                {
                    firing = null;
                }
            }
        }

        /// <summary>The answer taken: the listener (and the colony's own with them) to the colonist's side; with the play-outs off, at once.</summary>
        public static void Begin(HappeningContext ctx, Pawn helper)
        {
            var map = ctx?.map;
            var t = CouncilGameComponent.Instance?.happenings;
            var x = ctx?.stranger;
            var on = ctx?.b;
            if (map == null || t == null || x == null || on == null || ctx.faction == null) return;
            var v = t.AddVisit(Kind, ctx.incident, map, x, on, null, Now + 8 * GenDate.TicksPerHour);
            v.text = ctx.faction.GetUniqueLoadID() + "|" + (helper?.thingIDNumber ?? -1);
            if (PlayOuts.On && PeopleEvents9.Errand(x, map, on, PeopleEvents9.SitWithJob, PeopleEvents9Math.ShoutSitTicks, 7f))
            {
                if (helper != null && helper != on && PeoplesG.Up(helper, map)) PeopleEvents9.Send(helper, PeopleEvents9.SitWithJob, on, PeopleEvents9Math.ShoutSitTicks + GenDate.TicksPerHour);
                return;
            }
            Calmed(v);
        }

        private static Pawn Helper(Visit v)
        {
            var p = (v?.text ?? "").Split('|');
            int id = p.Length > 1 && int.TryParse(p[1], out int n) ? n : -1;
            return id < 0 ? null : v.map?.mapPawns?.FreeColonistsSpawned.FirstOrDefault(c => c.thingIDNumber == id);
        }

        public static bool Hour(Visit v, int now)
        {
            if (v.done) return false;
            var on = v.a;
            var x = v.x;
            // It ended before they were done: nothing more to do.
            if (on == null || on.Dead || !on.Spawned || on.Map != v.map || !on.InMentalState)
            {
                Passed(v);
                return false;
            }
            if (x != null && x.Spawned && x.CurJobDef == PeopleEvents9.SitWithJob && now <= v.until) return true;
            if (now > v.until || !PeoplesG.Up(x, v.map))
            {
                Passed(v);
                return false;
            }
            PeopleEvents9.Send(x, PeopleEvents9.SitWithJob, on, PeopleEvents9Math.ShoutSitTicks);
            return true;
        }

        /// <summary>The two hours at their side done (SitWith): the listener's sitting ends the break; false when it was no sitting of theirs.</summary>
        public static bool Sat(Pawn by, Pawn on)
        {
            var v = PeopleEvents9.Of(Kind, by);
            if (v == null || v.a != on)
                // The colony's own beside them: their part is the listener's.
                return by?.Faction != null && by.Faction.IsPlayer && OrderOn(on) != null;
            Calmed(v);
            return true;
        }

        /// <summary>The break over, calmly: the game's own recovery, the Choir's calm on them, the one who sat with them the wiser, told.</summary>
        private static void Calmed(Visit v)
        {
            if (v.done) return;
            v.done = true;
            var on = v.a;
            var x = v.x;
            var map = v.map;
            var f = x?.Faction ?? PeopleEvents9.PeopleOf(v);
            var helper = Helper(v);
            if (on != null && !on.Dead && on.InMentalState)
            {
                try
                {
                    on.MentalState.RecoverFromState();
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("peoples' events 9: the break ended", e);
                }
                PeopleEvents9.Thought(on, "Rimfolk_Thought_ChoirSatWith");
                PeoplesG.SayLine(x, v.incident, "calm");
                if (helper != null && !helper.Dead)
                {
                    helper.skills?.Learn(SkillDefOf.Social, PeopleEvents9Math.ShoutSocialXp, true);
                    if (helper.CurJobDef == PeopleEvents9.SitWithJob) helper.jobs.EndCurrentJob(JobCondition.Succeeded);
                }
                Tell.Message(NewsKind.Health, Importance.Notable, map,
                    (helper != null ? "CC_PEv9_ShoutCalmedWith" : "CC_PEv9_ShoutCalmed").Translate((x?.LabelShort ?? "?").Named("LISTENER"), on.Named("PAWN"), (helper?.LabelShort ?? "").Named("HELPER"))
                    .Resolve(), on.Spawned ? new LookTargets(on) : LookTargets.Invalid, MessageTypeDefOf.PositiveEvent);
                PeopleEvents9.Goodwill(f, PeopleEvents9Math.ShoutGoodwill, GoodwillWhy.TheirWork);
            }
            if (x != null) PeoplesG.Home(new List<Pawn> { x });
        }

        /// <summary>The break passed by itself, or the listener could not stay: they go home.</summary>
        private static void Passed(Visit v)
        {
            if (v.done) return;
            v.done = true;
            var helper = Helper(v);
            if (helper != null && helper.CurJobDef == PeopleEvents9.SitWithJob) helper.jobs.EndCurrentJob(JobCondition.Succeeded);
            if (v.map != null)
                Tell.Message(NewsKind.People, Importance.Notable, v.map, "CC_PEv9_ShoutPassed".Translate((v.x?.LabelShort ?? "?").Named("LISTENER"), (v.a?.LabelShort ?? "?").Named("PAWN")).Resolve(),
                    LookTargets.Invalid, MessageTypeDefOf.NeutralEvent);
            if (v.x != null) PeoplesG.Home(new List<Pawn> { v.x });
        }
    }

    /// <summary>
    /// A listener of {FACTION} ({X}) at the gate: they heard {B} from the road, in a long break, and would sit beside {B} until it passes.
    /// Fired from code (MindShouts.Watch); it doesn't come on the storyteller's draw.
    /// </summary>
    public class HappeningWorker_MindShouts : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) => PeoplesD.Friendly(PeoplesMath.Listeners) != null && MindShouts.For(map) != null;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Listeners);
            ctx.b = MindShouts.For(ctx.map);
            if (ctx.faction == null || ctx.b == null) return false;
            ctx.tale = ctx.b.MentalState?.def?.label ?? "?";
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Choir_Listener"), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            ctx.a = PeoplesD.Best(ctx.map, SkillDefOf.Social, p => p != ctx.b && !p.InMentalState);
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return true;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            PeopleEvents.AgeTags(tags, ctx.faction);
            if (ctx.a == null) tags.Add("alone");
        }

        public override string Unavailable(HappeningContext ctx, int option)
        {
            if (option > 1) return null;
            if (ctx.b == null || ctx.b.Dead || !ctx.b.Spawned || ctx.b.Map != ctx.map || !ctx.b.InMentalState) return "CC_PEv9_ShoutOver".Translate().Resolve();
            if (option == 1 && (ctx.a == null || ctx.a.Dead || !ctx.a.Spawned || ctx.a.InMentalState)) return "CC_PEv9_NoHelper".Translate().Resolve();
            return null;
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option <= 1 ? "sit" : "later");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The listener to {B}'s side for two hours (MindShouts.Begin); with <see cref="helper"/>, {A} sits with them and learns how.</summary>
    public class DilemmaEffect_MindShouts : DilemmaEffect
    {
        public bool helper;

        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc?.b == null) return;
            if (PlayOuts.On) sheet.Play("CC_PEv9_Impact_ShoutSit".Translate(hc.stranger?.LabelShort ?? "?", hc.b.LabelShort).Resolve(), "CC_PEv9_Impact_ShoutSitBrief".Translate().Resolve());
            sheet.Add(ImpactKind.Mood, 1, "CC_PEv9_Impact_ShoutEnds".Translate(hc.b.LabelShort).Resolve(), "CC_PEv9_Impact_ShoutEndsBrief".Translate().Resolve(), "pev9shout");
            if (helper && hc.a != null)
                sheet.Add(ImpactKind.Skill, 1, "CC_PEv9_Impact_ShoutLearn".Translate(hc.a.LabelShort, SkillDefOf.Social.label).Resolve(), "CC_PEv9_Impact_ShoutLearnBrief".Translate().Resolve(), "pev9shoutlearn");
            if (hc.faction != null) sheet.Goodwill(hc.faction, PeopleEvents9Math.ShoutGoodwill);
        }

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            MindShouts.Begin(hc, helper ? hc?.a : null);
        }
    }
}
