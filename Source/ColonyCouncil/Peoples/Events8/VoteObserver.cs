using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    // ===================== 71. An observer at the vote (the Last Republic) =====================

    /// <summary>
    /// An observer at the vote (item 71): on the morning of the colony's polling day (Offices/OfficesElection.cs, a contested
    /// election), a clerk of the Last Republic comes to the gate with the roll book and asks to observe. Let in, the clerk walks the
    /// colony with the roll (a word with each voter at home, eight at most), waits by the meeting place, and stands by the count in
    /// the evening. While the clerk is there a heckle at the count's speeches comes to blows half as often (Harm/HustingsBrawls.cs).
    /// When the result is in, the clerk says the vote was fair and goes, and with a radio at home (a powered comms console, or the
    /// Signal Line's mast while it sings) the colony hears the Republic's evening broadcast speak of a fair vote. With the second
    /// answer the result goes into the Republic's register, and the colony's officers have a good memory of it. A count put off past
    /// the clerk's patience, or held at the table without a gathering, sends the clerk home without a word on the radio. One of the
    /// events' visits (kind "pev:vote"): x the clerk, its text "people|election", stake 1 for the register (and 2 once the count was
    /// seen), due when it began.
    /// </summary>
    public static class VoteObserver
    {
        public const string Kind = "pev:vote";
        public const string EventDef = "Rimfolk_Evt_VoteObserver";

        private static int Now => Find.TickManager.TicksGame;

        /// <summary>The colony's election under way at this home that a clerk could watch: contested, not a ceremony, not over.</summary>
        public static Election Open(Map map)
        {
            var e = OfficeTracker.For(map)?.election;
            return e != null && e.map == map && !e.ceremony && e.Contested ? e : null;
        }

        public static string Key(Election e) => e?.map == null ? null : e.map.uniqueID + ":" + e.announcedTick;

        private static string KeyOf(Visit v)
        {
            var p = (v?.text ?? "").Split('|');
            return p.Length > 1 ? p[1] : null;
        }

        /// <summary>A clerk of the Republic is at this home, observing its vote (the hustings' blows half as often).</summary>
        public static bool Watching(Map map) =>
            map != null && (CouncilGameComponent.Instance?.happenings?.visits?.Any(v => v != null && v.kind == Kind && !v.done && v.map == map && v.x != null && v.x.Spawned && v.x.Map == map) ?? false);

        /// <summary>The colony's people on the roll: at home, grown, free.</summary>
        public static List<Pawn> Roll(Map map, Pawn from) =>
            IncidentWorker_Happening.Colonists(map).Where(p => !p.DevelopmentalStage.Juvenile() && !p.IsSlave && !p.Downed)
                .OrderBy(p => from != null && from.Spawned ? p.Position.DistanceToSquared(from.Position) : 0).Take(PeopleEvents8Math.RollMax).ToList();

        /// <summary>
        /// The events' hour at a home: on polling day morning, once an election, a Republic at peace, not soon after the last observer,
        /// at a chance (the storytellers' lean on visitors), a clerk comes.
        /// </summary>
        public static void Watch(Map map, int now)
        {
            var b = PeopleEvents8Book.Instance;
            var e = Open(map);
            if (b == null || e == null || e.gathering) return;
            string key = Key(e);
            if (b.ElectionRolled(key) || !PeopleEvents8Math.ObserverDue(now, e.pollTick, e.countTick)) return;
            if (!Tell.CanAsk || MapFacts.ThreatOn(map)) return;
            b.NoteElection(key);
            var f = PeoplesD.Friendly(PeoplesMath.Citizens);
            if (f == null || b.lastObserver >= 0 && now - b.lastObserver < PeopleEvents8Math.ObserverGapDays * GenDate.TicksPerDay) return;
            if (!Rand.Chance(PeopleEvents8Math.ObserverChance * StoryDirection.Factor(StoryWaysMath.Visitors))) return;
            if (PeopleEvents2.Fire(EventDef, map, f)) b.lastObserver = now;
        }

        /// <summary>The answer taken: the clerk walks the roll, then waits by the meeting place for the count; with the play-outs off, waits at once.</summary>
        public static void Begin(HappeningContext ctx, bool register)
        {
            var map = ctx?.map;
            var t = CouncilGameComponent.Instance?.happenings;
            var x = ctx?.stranger;
            var e = Open(map);
            if (map == null || t == null || x == null || ctx.faction == null) return;
            if (e == null)
            {
                Arrivals.SendAway(new List<Pawn> { x });
                return;
            }
            var v = t.AddVisit(Kind, ctx.incident, map, x, ctx.a, null, e.countTick + PeopleEvents8Math.ObserverWaitHours * GenDate.TicksPerHour);
            v.text = ctx.faction.GetUniqueLoadID() + "|" + Key(e);
            v.stake = register ? 1 : 0;
            v.due = Now;
            if (!PeoplesG.Up(x, map)) return;
            // Kept at the meeting place until well after the count, so the visit's own duties don't send the clerk home early.
            var spot = e.spot.IsValid ? e.spot : PeoplesD.GateSpot(map) ?? x.Position;
            Lords.Leave(x);
            Verse.AI.Group.LordMaker.MakeNewLord(x.Faction, new LordJob_VisitOnErrand(x.Faction, spot, Mathf.Max(GenDate.TicksPerHour, v.until - Now)), map, new List<Pawn> { x });
            if (!PlayOuts.On) return;
            // The voters the clerk can get to: one in a room visitors keep out of (a bedroom, a cold store) is passed over, here and on the
            // walk (JobDriver_PEv8Work), so the roll goes on past them.
            var roll = Roll(map, x).Where(p => x.CanReach(p, PathEndMode.Touch, Danger.Some)).Select(p => (LocalTargetInfo)p).ToList();
            if (roll.Count > 0) PeopleEvents8.Send(x, PeopleEvents8.RollJob, roll[0], PeopleEvents8Math.RollTicks, roll.Skip(1).ToList());
        }

        /// <summary>A word with a voter on the roll done: a nod and their name checked off.</summary>
        public static void Rolled(Pawn clerk, Pawn voter)
        {
            if (voter == null || !voter.Spawned || clerk == null) return;
            if (Rand.Chance(0.35f)) PeoplesG.SayLine(clerk, Of(clerk)?.incident, "roll");
        }

        private static Visit Of(Pawn clerk) => PeopleEvents8.Of(Kind, clerk);

        /// <summary>
        /// The visit's hour: the clerk stands by the count while it is read; once the result is in, the verdict; the election ended
        /// another way (no count to watch), or the clerk's patience gone, they go home without one.
        /// </summary>
        public static bool Hour(Visit v, int now)
        {
            if (v.done) return false;
            var x = v.x;
            var map = v.map;
            var t = OfficeTracker.For(map);
            var e = t?.election;
            bool same = e != null && Key(e) == KeyOf(v);
            if (same && e.gathering && e.phase == ElectionPhase.Count)
            {
                v.stake |= 2;
                if (PeoplesG.Up(x, map) && x.CurJobDef != PeopleEvents8.StandByJob)
                {
                    var cell = e.spot.IsValid ? CellFinder.StandableCellNear(e.spot, map, 4f) : IntVec3.Invalid;
                    if (cell.IsValid) PeopleEvents8.Send(x, PeopleEvents8.StandByJob, cell, 5 * GenDate.TicksPerHour);
                }
            }
            if (!same)
            {
                if ((v.stake & 2) != 0 && t != null && t.lastElectionTick >= v.due) Counted(v);
                else Gone(v, "nocount");
                return false;
            }
            if (x == null || x.Dead || !x.Spawned || x.Map != map)
            {
                v.done = true;
                return false;
            }
            if (now > v.until)
            {
                Gone(v, "late");
                return false;
            }
            return true;
        }

        /// <summary>The result in: the clerk calls it fair; the radio says so where it is heard; the register, with the second answer.</summary>
        private static void Counted(Visit v)
        {
            v.done = true;
            var x = v.x;
            var map = v.map;
            var f = x?.Faction ?? PeopleEvents8.PeopleOf(v);
            bool register = (v.stake & 1) != 0;
            if (x != null && x.Spawned) PeoplesG.SayLine(x, v.incident, "fair");
            string colony = Find.FactionManager.OfPlayer?.Name ?? "?";
            if (f != null && RepublicRadio.HasRadio(map))
            {
                var console = map.listerBuildings.allBuildingsColonist.FirstOrDefault(b => b.def == ThingDefOf.CommsConsole) ?? RelayMast.Of(map);
                Tell.Letter(NewsKind.World, Importance.Notable, map, "CC_PEv8_FairRadioLabel".Translate(f.Name).Resolve(),
                    "CC_PEv8_FairRadio".Translate(f.Name.Named("FACTION"), colony.Named("COLONY"), (x?.LabelShort ?? "?").Named("CLERK")).Resolve(), LetterDefOf.PositiveEvent,
                    console != null ? new LookTargets(console) : LookTargets.Invalid, f);
                foreach (var p in IncidentWorker_Happening.Colonists(map).Where(p => !p.DevelopmentalStage.Baby())) PeopleEvents8.Thought(p, "Rimfolk_Thought_FairVote");
            }
            else
                Tell.Message(NewsKind.World, Importance.Notable, map, "CC_PEv8_FairNoRadio".Translate((x?.LabelShort ?? "?").Named("CLERK"), (f?.Name ?? "?").Named("FACTION")).Resolve(),
                    x != null && x.Spawned ? new LookTargets(x) : LookTargets.Invalid, MessageTypeDefOf.PositiveEvent);
            int goodwill = PeopleEvents8Math.ObserverGoodwill;
            if (register)
            {
                var t = OfficeTracker.For(map);
                var seated = t == null ? new List<Pawn>() : OfficeTracker.Offices.SelectMany(o => t.HoldersOf(o)).Where(p => p != null && !p.Dead).Distinct().ToList();
                foreach (var p in seated) PeopleEvents8.Thought(p, "Rimfolk_Thought_InTheRegister");
                if (seated.Count > 0)
                    Tell.Message(NewsKind.World, Importance.Flavor, map, "CC_PEv8_Registered".Translate((f?.Name ?? "?").Named("FACTION"), TextUtil.JoinAnd(seated.Select(p => p.LabelShort).ToList()).Named("NAMES")).Resolve(),
                        new LookTargets(seated.Where(p => p.Spawned)), MessageTypeDefOf.PositiveEvent);
                goodwill += PeopleEvents8Math.RegisterGoodwill;
            }
            PeopleEvents8.Goodwill(f, goodwill, GoodwillWhy.Welcome);
            if (x != null) Arrivals.SendAway(new List<Pawn> { x });
        }

        /// <summary>No count the clerk could watch (it came to nothing, or was read at the table), or the clerk could wait no longer.</summary>
        private static void Gone(Visit v, string why)
        {
            v.done = true;
            var x = v.x;
            if (x == null || !x.Spawned) return;
            PeoplesG.SayLine(x, v.incident, why);
            Tell.Message(NewsKind.World, Importance.Flavor, v.map, ("CC_PEv8_Observer_" + why).Translate(x.LabelShort.Named("CLERK")).Resolve(), new LookTargets(x), MessageTypeDefOf.NeutralEvent);
            Arrivals.SendAway(new List<Pawn> { x });
        }

        /// <summary>Dev mode: a clerk to observe the vote now (when an election is under way here).</summary>
        public static string Now_(Map map)
        {
            var f = PeoplesD.Friendly(PeoplesMath.Citizens);
            var e = Open(map);
            if (f == null) return "no Last Republic at peace.";
            if (e == null) return "no contested election under way here (call one with the council's tools first).";
            PeopleEvents8Book.Instance?.NoteElection(Key(e));
            return PeopleEvents2.Fire(EventDef, map, f) ? "a clerk of " + f.Name + " comes to observe the vote." : "the clerk could not come now (a question waits, or a threat is on).";
        }
    }

    /// <summary>
    /// A clerk of {FACTION} ({X}) at the gate on polling day: the Republic would observe the colony's vote, walk the colony with the roll
    /// ({COUNT} on it) and stand by the count this evening.
    /// </summary>
    public class HappeningWorker_VoteObserver : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) => PeoplesD.Friendly(PeoplesMath.Citizens) != null && VoteObserver.Open(map) != null && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Citizens);
            var e = VoteObserver.Open(ctx.map);
            if (ctx.faction == null || e == null) return false;
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Republic_Clerk"), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            ctx.count = VoteObserver.Roll(ctx.map, null).Count;
            ctx.a = OfficeTracker.For(ctx.map)?.Speaker ?? KinMemory.Greeter(ctx.faction, ctx.map) ?? PeoplesD.Able(ctx.map).FirstOrDefault();
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            PeopleEvents.AgeTags(tags, ctx.faction);
            if (Papers.Holds(ctx.faction)) tags.Add("papers");
        }

        public override string Unavailable(HappeningContext ctx, int option) =>
            option <= 1 && VoteObserver.Open(ctx.map) == null ? "CC_PEv8_VoteGone".Translate().Resolve() : null;

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option <= 1 ? "observe" : "later");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The clerk walks the roll and stands by the count (VoteObserver.Begin); with <see cref="register"/>, the result goes into the Republic's register.</summary>
    public class DilemmaEffect_VoteObserver : DilemmaEffect
    {
        public bool register;

        public override bool ChanceEach => false;

        public override string Unavailable(DilemmaContext ctx) => VoteObserver.Open(ctx?.map) == null ? "CC_PEv8_VoteGone".Translate().Resolve() : null;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc?.stranger == null || hc.map == null) return;
            sheet.Play("CC_PEv8_Impact_Roll".Translate(hc.stranger.LabelShort, hc.count).Resolve(), "CC_PEv8_Impact_RollBrief".Translate().Resolve());
            if (Harm.On && Harm.SystemOn(HarmKinds.Hustings.system))
                sheet.Add(ImpactKind.Health, 1, "CC_PEv8_Impact_Calmer".Translate().Resolve(), "CC_PEv8_Impact_CalmerBrief".Translate().Resolve(), "pev8calmer");
            if (RepublicRadio.HasRadio(hc.map))
                sheet.Add(ImpactKind.Mood, 1, "CC_PEv8_Impact_FairRadio".Translate().Resolve(), "CC_PEv8_Impact_FairRadioBrief".Translate().Resolve(), "pev8fair");
            if (register)
                sheet.Add(ImpactKind.Mood, 1, "CC_PEv8_Impact_Register".Translate().Resolve(), "CC_PEv8_Impact_RegisterBrief".Translate().Resolve(), "pev8register");
            if (hc.faction != null) sheet.Goodwill(hc.faction, PeopleEvents8Math.ObserverGoodwill + (register ? PeopleEvents8Math.RegisterGoodwill : 0));
        }

        public override void Apply(DilemmaContext ctx) => VoteObserver.Begin(ctx as HappeningContext, register);
    }
}
