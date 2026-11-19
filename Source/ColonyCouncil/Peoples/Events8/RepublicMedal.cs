using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    // ===================== 72. A medal (the Last Republic) =====================

    /// <summary>
    /// A medal (item 72, a gift): when one of the colony does a great deed in a fight at home (the deeds the colony talks of,
    /// Social/Gossip/Deeds.cs: carried someone out of the fight, held the line in a hard one), word reaches the Last Republic, and half a
    /// day to four days later a clerk and a guard of the Civic Guard come to the gate to read a citation for them. The colonist walks out
    /// to the gate, the clerk reads it with the guard standing by, and pins on the medal: a lasting memory for them, the Republic
    /// remembering them by name for it (a deed of the peoples' memory, which the colony's renown reads), and from the industrial age on
    /// with Vanilla Books Expanded the Republic's newspaper with the citation printed in it. With the second answer the colony's people
    /// who are free come to the gate to hear it. Once a colonist, at a chance, thirty days apart. One of the events' visits (kind
    /// "pev:medal"): x the clerk, a the colonist, group the guard, its text "people|deed", stake 1 with the colony gathered.
    /// </summary>
    public static class RepublicMedal
    {
        public const string Kind = "pev:medal";
        public const string EventDef = "Rimfolk_Evt_RepublicMedal";
        public const string GiftKind = "medal";

        private static int Now => Find.TickManager.TicksGame;

        /// <summary>The deed the event is being fired for (set only while it is).</summary>
        private static Deed firing;

        private static Pawn Doer(Deed d) => d == null ? null : Reputations.Tracker?.PawnOf(d.doer);

        /// <summary>A colonist at this home who could be honoured for this deed now.</summary>
        private static bool Honourable(Deed d, Map map)
        {
            var p = Doer(d);
            return p != null && !p.Dead && p.Spawned && p.Map == map && p.IsFreeColonist && !p.DevelopmentalStage.Juvenile() && PeopleEvents.MayGive(GiftKind, p);
        }

        /// <summary>The freshest deed a medal is for at this home.</summary>
        public static Deed Fresh(Map map)
        {
            var tr = Reputations.Tracker;
            if (!Reputations.On || tr == null || map == null) return null;
            int now = Now;
            return tr.deeds.Where(d => d?.def != null && PeopleEvents8Math.MedalDeed(d.def.defName) && PeopleEvents8Math.MedalFresh(now, d.tick) && Honourable(d, map))
                .OrderByDescending(d => d.tick).FirstOrDefault();
        }

        /// <summary>The deed the event is for: the one the watch fires it for, else the freshest at this home.</summary>
        public static Deed For(Map map) => firing != null && Honourable(firing, map) ? firing : Fresh(map);

        /// <summary>
        /// The events' hour: a deed a medal is for, half a day old or more and still fresh, its doer at home, is rolled for once: at a
        /// chance (the storytellers' lean on visitors), a Republic at peace, not soon after the last medal, the clerk comes.
        /// </summary>
        public static void Watch(int now)
        {
            var b = PeopleEvents8Book.Instance;
            var tr = Reputations.Tracker;
            if (b == null || tr == null || !Reputations.On) return;
            foreach (var d in tr.deeds.Where(d => d?.def != null && PeopleEvents8Math.MedalDeed(d.def.defName) && !b.DeedRolled(d.id)).ToList())
            {
                if (now - d.tick > PeopleEvents8Math.MedalFreshDays * GenDate.TicksPerDay)
                {
                    b.NoteDeed(d.id);
                    continue;
                }
                if (!PeopleEvents8Math.MedalFresh(now, d.tick)) continue;
                var p = Doer(d);
                var map = p?.MapHeld;
                if (p == null || p.Dead || map == null || !Homes.IsHome(map) || !Honourable(d, map)) continue;
                if (!Tell.CanAsk || MapFacts.ThreatOn(map)) continue;
                b.NoteDeed(d.id);
                var f = PeoplesD.Friendly(PeoplesMath.Citizens);
                if (f == null || b.lastMedal >= 0 && now - b.lastMedal < PeopleEvents8Math.MedalGapDays * GenDate.TicksPerDay) continue;
                if (!Rand.Chance(PeopleEvents8Math.MedalChance * StoryDirection.Factor(StoryWaysMath.Visitors))) continue;
                if (Fire(d, map, f)) b.lastMedal = now;
            }
        }

        private static bool Fire(Deed d, Map map, Faction f)
        {
            firing = d;
            try
            {
                return PeopleEvents2.Fire(EventDef, map, f);
            }
            finally
            {
                firing = null;
            }
        }

        /// <summary>The answer taken: the colonist to the gate (and the colony with them), the clerk reads the citation; with the play-outs off, at once.</summary>
        public static void Begin(HappeningContext ctx, bool gather)
        {
            var map = ctx?.map;
            var t = CouncilGameComponent.Instance?.happenings;
            var x = ctx?.stranger;
            var a = ctx?.a;
            if (map == null || t == null || x == null || a == null || ctx.faction == null) return;
            var v = t.AddVisit(Kind, ctx.incident, map, x, a, ctx.group, Now + 8 * GenDate.TicksPerHour);
            v.text = ctx.faction.GetUniqueLoadID() + "|" + (ctx.tale ?? "").Replace("|", "/");
            v.stake = gather ? 1 : 0;
            if (PlayOuts.On && PeoplesG.Up(x, map) && PeoplesG.Up(a, map) && !a.Drafted && !a.InMentalState
                && PeopleEvents8.Errand(x, map, a, null, PeopleEvents8.CitationJob, PeopleEvents8Math.CitationTicks, 6f))
            {
                // The colonist walks out to the clerk, the guard stands by, and with the second answer the colony's free people come to hear it.
                var near = CellFinder.StandableCellNear(x.Position, map, 2f);
                if (near.IsValid) PeopleEvents8.Send(a, PeopleEvents8.StandByJob, near, PeopleEvents8Math.CitationTicks + GenDate.TicksPerHour);
                foreach (var g in ctx.group.Where(g => PeoplesG.Up(g, map)))
                {
                    var c = CellFinder.StandableCellNear(x.Position, map, 3f);
                    if (c.IsValid) PeopleEvents8.Send(g, PeopleEvents8.StandByJob, c, PeopleEvents8Math.CitationTicks + GenDate.TicksPerHour);
                }
                if (gather)
                    foreach (var p in PeoplesD.Able(map).Where(p => p != a && !p.Drafted && !p.InMentalState && PlayOuts.Interruptible(p)).Take(8))
                    {
                        var c = CellFinder.StandableCellNear(x.Position, map, 5f);
                        if (c.IsValid) PeopleEvents8.Send(p, PeopleEvents8.StandByJob, c, PeopleEvents8Math.CitationTicks + GenDate.TicksPerHour);
                    }
                return;
            }
            Award(v);
        }

        /// <summary>The visit's hour: the clerk gone or the hours run out before the citation is read, it is read there and then.</summary>
        public static bool Hour(Visit v, int now)
        {
            if (v.done) return false;
            var x = v.x;
            bool reading = x != null && x.Spawned && x.CurJobDef == PeopleEvents8.CitationJob;
            if (reading && now <= v.until) return true;
            Award(v);
            return false;
        }

        /// <summary>The clerk's hour at the colonist's side done: the medal.</summary>
        public static void Read(Pawn clerk)
        {
            var v = PeopleEvents8.Of(Kind, clerk);
            if (v != null) Award(v);
        }

        /// <summary>
        /// The citation read and the medal pinned on, once: the colonist's lasting memory, the Republic's memory of them by name, the
        /// newspaper, and the colony's people near enough to hear it; told, and the clerk and the guard go home.
        /// </summary>
        private static void Award(Visit v)
        {
            if (v.done) return;
            v.done = true;
            var x = v.x;
            var a = v.a;
            var map = v.map;
            var f = x?.Faction ?? PeopleEvents8.PeopleOf(v);
            var party = v.group.Prepend(x).Where(p => p != null).ToList();
            if (a == null || a.Dead)
            {
                Arrivals.SendAway(party);
                return;
            }
            var parts = (v.text ?? "").Split('|');
            string deed = parts.Length > 1 ? parts[1] : "";
            PeopleEvents8.Thought(a, "Rimfolk_Thought_RepublicMedal");
            PeopleEventsBook.Gave(GiftKind, a);
            if (f != null) KinMemory.Note(f, GiftKind, a, deed);
            PeoplesG.SayLine(x, v.incident, "citation");
            if (a.Spawned) Speech.Say(a, TextVariants.Vary("CC_PEv8_MedalBack").Translate().Resolve());
            // Those near enough to hear it read: the colony's people at the gate.
            int heard = 0;
            if (x != null && x.Spawned)
                foreach (var p in IncidentWorker_Happening.Colonists(map).Where(p => p != a && p.Spawned && p.Position.InHorDistOf(x.Position, 9f) && !p.DevelopmentalStage.Baby()))
                {
                    PeopleEvents8.Thought(p, "Rimfolk_Thought_HeardCitation");
                    heard++;
                }
            bool paper = false;
            var paperDef = DefDatabase<ThingDef>.GetNamedSilentFail("VBE_Newspaper");
            if (paperDef != null && PeopleEvents8Math.Newspaper(PeopleEvents.AgeOf(f), true))
            {
                try
                {
                    var at = a.Spawned ? a.Position : x != null && x.Spawned ? x.Position : PeoplesD.GateSpot(map) ?? IntVec3.Invalid;
                    if (at.IsValid && map != null)
                    {
                        var news = ThingMaker.MakeThing(paperDef, paperDef.MadeFromStuff ? GenStuff.DefaultStuffFor(paperDef) : null);
                        paper = GenPlace.TryPlaceThing(news, at, map, ThingPlaceMode.Near);
                    }
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("peoples' events 8: the Republic's newspaper", e);
                }
            }
            string key = paper ? "CC_PEv8_MedalPaper" : "CC_PEv8_Medal";
            Tell.Message(NewsKind.People, Importance.Notable, map, key.Translate((x?.LabelShort ?? "?").Named("CLERK"), a.Named("PAWN"), (f?.Name ?? "?").Named("FACTION"), deed.Named("DEED"),
                heard.Named("COUNT")).Resolve(), a.Spawned ? new LookTargets(a) : LookTargets.Invalid, MessageTypeDefOf.PositiveEvent);
            PeopleEvents8.Goodwill(f, PeopleEvents8Math.MedalGoodwill, GoodwillWhy.Welcome);
            Arrivals.SendAway(party);
        }

        /// <summary>Dev mode: the clerk with a medal now, for the freshest deed here (one recorded for the best shot if none).</summary>
        public static string Now_(Map map)
        {
            var f = PeoplesD.Friendly(PeoplesMath.Citizens);
            if (map == null || f == null) return "no Last Republic at peace.";
            if (!Reputations.On || Reputations.Tracker == null) return "the colony's talk of deeds is off (the setting), so no deed to honour.";
            var d = Fresh(map);
            if (d == null)
            {
                var p = PeoplesD.Best(map, SkillDefOf.Shooting, x => PeopleEvents.MayGive(GiftKind, x));
                if (p == null) return "nobody here could have a medal now (a gift came lately).";
                d = Deeds.Record("Rimfolk_Deed_HeldLine", p, null, -1f, null, null, null, false);
                if (d == null) return "no deed could be recorded.";
                d.tick = Now - GenDate.TicksPerDay;
            }
            PeopleEvents8Book.Instance?.NoteDeed(d.id);
            return Fire(d, map, f) ? "a clerk of " + f.Name + " comes with a medal for " + (Doer(d)?.LabelShort ?? "?") + "." : "the clerk could not come now (a question waits, or a threat is on).";
        }
    }

    /// <summary>
    /// A clerk of {FACTION} ({X}) and a guard at the gate: the Republic would read a citation for {A}, who {TALE} in {RELATION} (the fight's
    /// name), and pin its medal on {A_objective}.
    /// </summary>
    public class HappeningWorker_RepublicMedal : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) => PeoplesD.Friendly(PeoplesMath.Citizens) != null && RepublicMedal.For(map) != null;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Citizens);
            var d = RepublicMedal.For(ctx.map);
            if (ctx.faction == null || d == null) return false;
            ctx.a = Reputations.Tracker?.PawnOf(d.doer);
            if (ctx.a == null) return false;
            ctx.tale = Deeds.TextOf(d, ctx.a);
            ctx.relation = d.label ?? "";
            ctx.note = d.def.defName;
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Republic_Clerk"), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            var guard = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Republic_Guard"), ctx.faction, new FloatRange(20f, 45f));
            if (guard != null) ctx.group.Add(guard);
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return true;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            PeopleEvents.AgeTags(tags, ctx.faction);
            tags.Add(ctx.note == "Rimfolk_Deed_Rescue" ? "rescue" : "line");
        }

        public override string Unavailable(HappeningContext ctx, int option) =>
            option <= 1 && (ctx.a == null || ctx.a.Dead || !ctx.a.Spawned || ctx.a.Map != ctx.map) ? "CC_PEv8_MedalAway".Translate(ctx.a?.LabelShort ?? "?").Resolve() : null;

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option <= 1 ? "come" : "later");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The citation read at the gate and the medal pinned on (RepublicMedal.Begin); with <see cref="gather"/>, the colony's free people come to hear it.</summary>
    public class DilemmaEffect_RepublicMedal : DilemmaEffect
    {
        public bool gather;

        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc?.a == null || hc.stranger == null) return;
            sheet.Play("CC_PEv8_Impact_Citation".Translate(hc.a.LabelShort, hc.stranger.LabelShort).Resolve(), "CC_PEv8_Impact_CitationBrief".Translate().Resolve());
            sheet.Add(ImpactKind.Mood, 1, "CC_PEv8_Impact_Medal".Translate(hc.a.LabelShort).Resolve(), "CC_PEv8_Impact_MedalBrief".Translate().Resolve(), "pev8medal");
            sheet.Add(ImpactKind.Other, 1, "CC_PEv8_Impact_MedalName".Translate(hc.a.LabelShort, hc.faction?.Name ?? "?").Resolve(), "CC_PEv8_Impact_MedalNameBrief".Translate().Resolve(),
                "pev8medalname");
            var paper = DefDatabase<ThingDef>.GetNamedSilentFail("VBE_Newspaper");
            if (paper != null && PeopleEvents8Math.Newspaper(PeopleEvents.AgeOf(hc.faction), true)) sheet.Goods(1, paper, 1);
            if (gather) sheet.Add(ImpactKind.Mood, 1, "CC_PEv8_Impact_Heard".Translate().Resolve(), "CC_PEv8_Impact_HeardBrief".Translate().Resolve(), "pev8heard");
            if (hc.faction != null) sheet.Goodwill(hc.faction, PeopleEvents8Math.MedalGoodwill);
        }

        public override void Apply(DilemmaContext ctx) => RepublicMedal.Begin(ctx as HappeningContext, gather);
    }
}
