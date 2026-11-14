using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    // ===================== 16. Two faiths at one table (the pilgrim fellowship) =====================

    /// <summary>
    /// Two faiths at one table (item 16): when two faiths in the colony quarrel (the faith clash: Dilemmas/DilemmaWorkers_Faith.cs)
    /// or its own faith is near a split (believers wavering, as Vanilla Ideology Expanded - Splits and Schisms splits a faith),
    /// a palmer of the fellowship comes. That evening the colony gathers (the gathering play-out, the palmer among them) and
    /// each side's best speaks. The blessing settles it for a season (certainty steadied on both sides, and the faiths' clash
    /// not asked again before then), or, by the speakers' Social on the card, the split comes anyway and is said plainly (the
    /// wavering shaken further). What it does waits for the evening's end (Interplay.Notify_GatheringOver).
    /// </summary>
    public static class TwoFaiths
    {
        private static int Now => Find.TickManager.TicksGame;

        /// <summary>The faiths' clash rests while the table's blessing holds (DilemmaWorker_FaithClash).</summary>
        public static bool Settled
        {
            get
            {
                var b = PeopleEvents3Book.Instance;
                return b != null && b.settledUntil >= 0 && Now < b.settledUntil;
            }
        }

        /// <summary>The colony's own largest faith near a split: three believers or more, a third of them wavering.</summary>
        public static bool NearSplit(out Ideo faith)
        {
            faith = null;
            if (!Faiths.On) return false;
            var largest = Faiths.Census().FirstOrDefault();
            if (largest?.ideo == null) return false;
            int wavering = largest.believers.Count(p => Faiths.Certainty(p) < PeopleEvents3Math.Waver);
            if (!PeopleEvents3Math.NearSplit(largest.believers.Count, wavering)) return false;
            faith = largest.ideo;
            return true;
        }

        /// <summary>Why the palmer comes now: "clash" (two faiths quarrel) or "split" (the colony's faith near one); null for neither, or while the blessing holds.</summary>
        public static string Why(Map map)
        {
            if (!Faiths.On || map == null || Settled) return null;
            if (FaithClash.Clashing() && FaithClash.Quarrel(map) != null) return "clash";
            return NearSplit(out _) ? "split" : null;
        }

        /// <summary>Whoever speaks for a side: of a faith, its speaker at home; of the wavering, the most Social of them.</summary>
        public static Pawn Wavering(Map map, Ideo faith) =>
            map.mapPawns.FreeColonistsSpawned.Where(p => Faiths.Of(p) == faith && Faiths.Certainty(p) < PeopleEvents3Math.Waver && StorytellerEvents.Able(p))
                .OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0).FirstOrDefault();

        /// <summary>
        /// The answer's evening noted (the gathering the answer set going is in play): what the roll gave, and the two sides,
        /// waiting for the evening's end.
        /// </summary>
        public static void Planned(HappeningContext ctx, bool split)
        {
            var b = PeopleEvents3Book.Instance;
            if (b == null || ctx == null) return;
            var play = PlayOuts.Collected().LastOrDefault(p => p.IsGathering);
            b.tablePlay = play?.id ?? -1;
            b.tableSplit = split;
            b.tableWhy = ctx.note;
            b.tableFaiths = (Faiths.Of(ctx.a)?.name ?? "") + "|" + (ctx.note == "clash" ? Faiths.Of(ctx.b)?.name ?? "" : "");
            b.tableSince = Now;
            b.tablePalmer = ctx.stranger;
            b.tableFaction = ctx.faction;
            // With answers not played out on the map, it is done at once.
            if (play == null) Conclude(b, ctx.map, ctx.map.mapPawns.FreeColonistsSpawned.ToList(), false);
        }

        /// <summary>A gathering is over (Interplay.Notify_GatheringOver): if it was the palmer's table, it does its work now.</summary>
        public static void GatheringOver(PlayOut play, List<Pawn> attended, bool early)
        {
            var b = PeopleEvents3Book.Instance;
            if (b == null || play == null || b.tablePlay < 0 || play.id != b.tablePlay) return;
            try
            {
                Conclude(b, play.map, attended ?? new List<Pawn>(), early);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 3: the table over", e);
            }
        }

        /// <summary>An evening that never came about (called off, the palmer gone): forgotten after two days.</summary>
        public static void Tidy()
        {
            var b = PeopleEvents3Book.Instance;
            if (b == null || b.tableSince < 0 || Now - b.tableSince < 2 * GenDate.TicksPerDay) return;
            b.tablePlay = -1;
            b.tableSince = -1;
            b.tablePalmer = null;
        }

        private static void Conclude(PeopleEvents3Book b, Map map, List<Pawn> attended, bool early)
        {
            var parts = (b.tableFaiths ?? "").Split('|');
            var one = parts.Length > 0 ? FaithEvenings.Named(parts[0]) : null;
            var two = parts.Length > 1 && parts[1].Length > 0 ? FaithEvenings.Named(parts[1]) : null;
            bool split = b.tableSplit;
            var f = b.tableFaction;
            var palmer = b.tablePalmer;
            b.tablePlay = -1;
            b.tableSince = -1;
            b.tablePalmer = null;
            var there = attended.Where(p => p != null && !p.Dead && Faiths.Holds(p) && (Faiths.Of(p) == one || Faiths.Of(p) == two)).ToList();
            string faiths = two != null ? "CC_PEv3_TwoFaithsNames".Translate((one?.name ?? "?").Named("ONE"), two.name.Named("TWO")).Resolve() : one?.name ?? "?";
            if (!split)
            {
                foreach (var p in there) Faiths.Steady(p, PeopleEvents3Math.Steadied);
                b.settledUntil = Now + PeopleEvents3Math.SettledDays * GenDate.TicksPerDay;
                if (f != null && f.HasGoodwill && !f.IsPlayer) WorldSituationWorker.Goodwill(f, PeopleEvents3Math.TableGoodwill, false);
                Tell.Message(NewsKind.People, Importance.Notable, map, "CC_PEv3_TableSettled".Translate(faiths.Named("FAITHS"), there.Count.Named("COUNT")).Resolve(),
                    palmer != null && palmer.Spawned ? new LookTargets(palmer) : LookTargets.Invalid, MessageTypeDefOf.PositiveEvent);
            }
            else
            {
                // Said plainly: no nearer at the end of it, and the wavering further off than before.
                var shaken = there.Where(p => Faiths.Certainty(p) < PeopleEvents3Math.Waver || two != null).ToList();
                foreach (var p in shaken) Faiths.Shake(p, PeopleEvents3Math.SplitShaken);
                Tell.Message(NewsKind.People, Importance.Important, map,
                    (two != null ? "CC_PEv3_TableSplitClash" : "CC_PEv3_TableSplit").Translate(faiths.Named("FAITHS"), (one?.name ?? "?").Named("FAITH")).Resolve(),
                    palmer != null && palmer.Spawned ? new LookTargets(palmer) : LookTargets.Invalid, MessageTypeDefOf.NegativeEvent);
            }
            if (palmer != null && palmer.Spawned)
            {
                PeoplesG.SayLine(palmer, DefDatabase<IncidentDef>.GetNamedSilentFail("Rimfolk_Evt_TwoFaiths"), split ? "split" : "settled");
                Arrivals.SendAway(new[] { palmer });
            }
        }

        /// <summary>
        /// The palmer's blessing table to table instead (the second answer): a smaller steadying for each of the two sides'
        /// believers she reaches as she walks about the colony, the clash left as it is.
        /// </summary>
        public static void Apart(HappeningContext ctx)
        {
            var x = ctx?.stranger;
            var t = CouncilGameComponent.Instance?.happenings;
            if (x == null || !x.Spawned || t == null) return;
            var one = Faiths.Of(ctx.a);
            var two = ctx.note == "clash" ? Faiths.Of(ctx.b) : null;
            var who = ctx.map.mapPawns.FreeColonistsSpawned.Where(p => Faiths.Holds(p) && (Faiths.Of(p) == one || Faiths.Of(p) == two)).Take(8).ToList();
            var v = t.AddVisit(BlessKind, ctx.incident, ctx.map, x, null, null, Now + 10 * GenDate.TicksPerHour);
            v.touched.AddRange(who);
            v.text = ctx.faction.GetUniqueLoadID();
        }

        public const string BlessKind = "pev:bless";

        /// <summary>The palmer's walk: to the next believer not yet blessed, a word and a steadying, then on; done, home.</summary>
        public static bool BlessHour(Visit v, int now)
        {
            if (v.done) return false;
            var x = v.x;
            if (x == null || x.Dead || !x.Spawned || now > v.until)
            {
                v.done = true;
                if (x != null && x.Spawned) Arrivals.SendAway(new[] { x });
                return false;
            }
            v.touched.RemoveAll(p => p == null || p.Dead || !p.Spawned || p.Map != x.Map);
            var next = v.touched.OrderBy(p => p.Position.DistanceToSquared(x.Position)).FirstOrDefault();
            if (next == null)
            {
                v.done = true;
                PeoplesG.SayLine(x, v.incident, "apart");
                Arrivals.SendAway(new[] { x });
                return false;
            }
            if (x.Position.InHorDistOf(next.Position, 3f))
            {
                v.touched.Remove(next);
                Faiths.Steady(next, PeopleEvents3Math.Steadied / 2f);
                PeoplesG.SayLine(x, v.incident, "bless");
                return true;
            }
            if (x.CurJobDef != JobDefOf.Goto && x.CanReach(next, PathEndMode.Touch, Danger.Some))
            {
                var go = JobMaker.MakeJob(JobDefOf.Goto, next);
                go.locomotionUrgency = LocomotionUrgency.Walk;
                x.jobs.StartJob(go, JobCondition.InterruptForced, null, false, true);
            }
            return true;
        }
    }

    /// <summary>
    /// A palmer of {FACTION} at the gate: {A}'s faith ({TALE}) and {B}'s ({DEAD}) quarrel, or {TALE} is near a split ({B}
    /// among its wavering); she would have the colony gather this evening, each side's best to speak.
    /// </summary>
    public class HappeningWorker_TwoFaiths : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            PeoplesD.Friendly(PeoplesMath.Pilgrims) != null && TwoFaiths.Why(map) != null && PlayOuts.On && Colonists(map).Count > 1;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Pilgrims);
            ctx.note = TwoFaiths.Why(ctx.map);
            if (ctx.faction == null || ctx.note == null) return false;
            if (ctx.note == "clash")
            {
                var q = FaithClash.Quarrel(ctx.map);
                if (q == null) return false;
                var one = Faiths.Of(q.Value.a);
                var two = Faiths.Of(q.Value.b);
                ctx.a = FaithEvenings.Speaker(ctx.map, one) ?? q.Value.a;
                ctx.b = FaithEvenings.Speaker(ctx.map, two) ?? q.Value.b;
                ctx.tale = one?.name;
                ctx.dead = two?.name;
            }
            else
            {
                if (!TwoFaiths.NearSplit(out var faith)) return false;
                ctx.b = TwoFaiths.Wavering(ctx.map, faith);
                ctx.a = ctx.map.mapPawns.FreeColonistsSpawned.Where(p => Faiths.Of(p) == faith && p != ctx.b && Faiths.Certainty(p) >= PeopleEvents3Math.Waver && StorytellerEvents.Able(p))
                    .OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0).FirstOrDefault() ?? FaithEvenings.Speaker(ctx.map, faith);
                ctx.tale = faith.name;
                ctx.dead = faith.name;
            }
            if (ctx.a == null || ctx.b == null || ctx.a == ctx.b) return false;
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Pilgrims_Keeper"), ctx.faction, ext.ages);
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return ctx.stranger != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            PeopleEvents.AgeTags(tags, ctx.faction);
            tags.Add(ctx.note ?? "clash");
        }

        public override string Unavailable(HappeningContext ctx, int option)
        {
            if (option == 0 && !PlayOuts.On) return "CC_PEv3_NoGathering".Translate().Resolve();
            return null;
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option == 0 ? "table" : option == 1 ? "walk" : "thanks");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The evening's verdict, kept for the gathering's end (TwoFaiths.Planned): settled, or with <see cref="split"/> the split comes anyway.</summary>
    public class DilemmaEffect_TwoFaiths : DilemmaEffect
    {
        public bool split;

        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc == null) return;
            if (split)
            {
                sheet.Add(ImpactKind.People, -1, "CC_PEv3_Impact_Split".Translate(hc.tale ?? "?").Resolve(), "CC_PEv3_Impact_SplitBrief".Translate().Resolve(), "pev3split");
                return;
            }
            sheet.Add(ImpactKind.People, 1, "CC_PEv3_Impact_Settle".Translate(PeopleEvents3Math.SettledDays, Mathf.RoundToInt(PeopleEvents3Math.Steadied * 100f)).Resolve(),
                "CC_PEv3_Impact_SettleBrief".Translate().Resolve(), "pev3settle");
            if (hc.faction != null) sheet.Goodwill(hc.faction, PeopleEvents3Math.TableGoodwill);
        }

        public override void Apply(DilemmaContext ctx) => TwoFaiths.Planned(ctx as HappeningContext, split);
    }

    /// <summary>The palmer blesses the two sides' believers one by one instead (TwoFaiths.Apart): a smaller steadying, the clash left as it is.</summary>
    public class DilemmaEffect_TwoFaithsApart : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            sheet.Play("CC_PEv3_Impact_Apart".Translate(ctx?.stranger?.LabelShort ?? "?").Resolve(), "CC_PEv3_Impact_ApartBrief".Translate().Resolve());
            sheet.Add(ImpactKind.People, 1, "CC_PEv3_Impact_ApartSteady".Translate(Mathf.RoundToInt(PeopleEvents3Math.Steadied * 50f)).Resolve(), "CC_PEv3_Impact_ApartSteadyBrief".Translate().Resolve(),
                "pev3apart");
        }

        public override void Apply(DilemmaContext ctx) => TwoFaiths.Apart(ctx as HappeningContext);
    }
}
