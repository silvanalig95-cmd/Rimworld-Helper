using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    // Two decrees that show on the map (the content update's A7, the player's go of 20 November 2026), run each morning by
    // the decrees' day (DecreeDef.daily, Decrees/DecreeWork.RunDaily):
    // - The night watch (six days): two of the colony's fighters, in turn by a rota so each walks as often as the others,
    //   walk the walls and the gate from nine until one (the patrolling vigil of the night watch, DilemmaEffect_Vigil), and
    //   sleep late the next morning (the late start, DilemmaEffect_Hours). What a night watch already halves (hungry
    //   strangers at the fields in a famine, Harm/GateTroubles.cs) it halves on those nights, and each night walked is the
    //   deed "kept the night watch", toward the Night-Watch name (Social/Talk/SharedTimes.cs).
    // - A welcome for newcomers (ten days): each evening a settled colonist who likes them spends an hour sitting with each
    //   newcomer (DilemmaEffect_Visit with sit, at a table where there is one); when it happened, it counts as welcomes for the group the newcomer is
    //   finding their way into (two from one of its own: Social/Groups/GroupNewcomers.cs), the host becomes the one who
    //   speaks for them there when they think well enough of them, and the newcomer's "somewhere to belong" story counts it
    //   as the welcome it waits for when the host is its friendly face.
    // The rules are NightWatchMath and WelcomeMath.

    /// <summary>The decree whose daily effects these are, and the day of it (0 the first).</summary>
    public static class DecreeDays
    {
        public static int DayOf(Map map, DilemmaEffect effect)
        {
            var t = DecreeTracker.For(map);
            var a = t?.active.FirstOrDefault(x => x?.def?.daily != null && x.def.daily.Contains(effect));
            return a == null ? 0 : Math.Max(0, (Find.TickManager.TicksGame - a.startTick) / GenDate.TicksPerDay);
        }
    }

    /// <summary>The night watch's rota for tonight: two fighters walk the walls and the gate, and sleep late after.</summary>
    public class DilemmaEffect_WatchRota : DilemmaEffect
    {
        public override bool ChanceEach => false;

        /// <summary>The colony's fighters at home who can walk a watch, in a fixed order (so the rota goes round them all).</summary>
        public static List<Pawn> Fighters(Map map) =>
            map?.mapPawns.FreeColonistsSpawned
                .Where(p => PlayOuts.OurPerson(p) && !p.IsQuestLodger() && !p.DevelopmentalStage.Juvenile() && !p.Downed && !p.WorkTagIsDisabled(WorkTags.Violent)
                            && !HealthAIUtility.ShouldSeekMedicalRest(p) && p.health.capacities.CapableOf(PawnCapacityDefOf.Moving))
                .OrderBy(p => p.thingIDNumber).ToList() ?? new List<Pawn>();

        /// <summary>Tonight's watchers by the rota (one or two).</summary>
        public static List<Pawn> Tonight(Map map, int day)
        {
            var fighters = Fighters(map);
            var (i, j) = NightWatchMath.Pair(day, fighters.Count);
            var list = new List<Pawn>();
            if (i >= 0) list.Add(fighters[i]);
            if (j >= 0 && j != i) list.Add(fighters[j]);
            return list;
        }

        private static DilemmaEffect_Vigil Watch(Map map) => new DilemmaEffect_Vigil
        {
            label = ("CC_Life_WatchLabel_" + Ages.Tag(Ages.Ours)).Translate().Resolve(), target = DilemmaTarget.AAndB, organizer = PlayWho.A, count = 2, to = PlaySpot.Gate,
            patrol = true, torches = Ages.Ours <= WorldAge.Medieval, hours = new IntRange(NightWatchMath.From, NightWatchMath.To),
            duration = new FloatRange(NightWatchMath.Hours, NightWatchMath.Hours)
        };

        private static DilemmaEffect_Hours LateStart() => new DilemmaEffect_Hours { plan = AnswerShiftMath.Late, target = DilemmaTarget.AAndB };

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (ctx?.map == null) return;
            var who = Tonight(ctx.map, DecreeDays.DayOf(ctx.map, this));
            if (who.Count == 0) return;
            var c = new DilemmaContext { map = ctx.map, a = who[0], b = who.Count > 1 ? who[1] : null, why = ctx.why };
            Watch(ctx.map).Describe(c, sheet);
            LateStart().Describe(c, sheet);
        }

        public override void Apply(DilemmaContext ctx)
        {
            if (ctx?.map == null || !PlayOuts.On) return;
            try
            {
                var who = Tonight(ctx.map, DecreeDays.DayOf(ctx.map, this));
                if (who.Count == 0) return;
                var c = new DilemmaContext { map = ctx.map, a = who[0], b = who.Count > 1 ? who[1] : null, why = ctx.why };
                if (Watch(ctx.map).Begin(c) == null) return;
                LateStart().Apply(c);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the night watch's rota", e);
            }
        }
    }

    /// <summary>A welcome for newcomers: each evening, an hour's sitting with each newcomer by a settled colonist who likes them.</summary>
    public class DilemmaEffect_WelcomeEvening : DilemmaEffect
    {
        public override bool ChanceEach => false;

        /// <summary>Its afterward on the map: the welcome counted (CouncilLife/WatchAndWelcome.cs, from PlayAfter.Run).</summary>
        public const string After = LifePlays.Prefix + "welcome";

        /// <summary>The colony's newcomers at home now.</summary>
        public static List<Pawn> Newcomers(Map map) =>
            map?.mapPawns.FreeColonistsSpawned.Where(p => PlayOuts.OurPerson(p) && !p.IsQuestLodger() && WelcomeMath.Newcomer(Life.DaysHere(p))).ToList() ?? new List<Pawn>();

        /// <summary>
        /// Who welcomes a newcomer tonight: the friendly face of their "somewhere to belong" story when there is one and they
        /// may, else the settled colonist who thinks best of them; not someone already welcoming another tonight.
        /// </summary>
        public static Pawn Host(Map map, Pawn newcomer, ICollection<Pawn> busy)
        {
            bool May(Pawn o) => o != null && o != newcomer && o.Spawned && o.Map == map && PlayOuts.OurPerson(o) && !o.DevelopmentalStage.Juvenile() && !o.Downed
                                && !busy.Contains(o) && WelcomeMath.CanWelcome(Life.DaysHere(o), ArcUtil.Opinion(o, newcomer));
            var arc = ArcTracker.Instance?.Of(newcomer);
            if (arc?.def?.Worker is ArcWorker_Newcomer && May(arc.other)) return arc.other;
            return map.mapPawns.FreeColonistsSpawned.Where(May).OrderByDescending(o => ArcUtil.Opinion(o, newcomer)).FirstOrDefault();
        }

        private static DilemmaEffect_Visit Hour(Pawn host, Pawn newcomer) => new DilemmaEffect_Visit
        {
            label = ("CC_Life_WelcomeLabel_" + Ages.Tag(Ages.Ours)).Translate(host.Named("PAWN"), newcomer.Named("OTHER")).Resolve(),
            who = PlayWho.A, whom = PlayWho.B, to = PlaySpot.Person, sit = true, sitBy = PlaySpot.Dining,
            hours = new IntRange(WelcomeMath.From, WelcomeMath.To), duration = new FloatRange(1f, 1.25f)
        };

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (ctx?.map == null) return;
            var busy = new HashSet<Pawn>();
            foreach (var n in Newcomers(ctx.map))
            {
                var host = Host(ctx.map, n, busy);
                if (host == null) continue;
                busy.Add(host);
                Hour(host, n).Describe(new DilemmaContext { map = ctx.map, a = host, b = n, why = ctx.why }, sheet);
            }
        }

        public override void Apply(DilemmaContext ctx)
        {
            if (ctx?.map == null || !PlayOuts.On) return;
            try
            {
                var busy = new HashSet<Pawn>();
                foreach (var n in Newcomers(ctx.map))
                {
                    var host = Host(ctx.map, n, busy);
                    if (host == null) continue;
                    var play = Hour(host, n).Begin(new DilemmaContext { map = ctx.map, a = host, b = n, why = ctx.why });
                    if (play == null) continue;
                    busy.Add(host);
                    play.afterward = After;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("a welcome for newcomers", e);
            }
        }

        /// <summary>
        /// The hour was sat: welcomes for the group the newcomer is finding their way into, the host speaking for them there
        /// when they think well of them, and the newcomer's "somewhere to belong" story's welcome when the host is its friendly face.
        /// </summary>
        public static void Over(PlayOut p, bool happened)
        {
            var host = p?.visitor;
            var newcomer = p?.other;
            if (!happened || host == null || newcomer == null || host.Dead || newcomer.Dead) return;
            var groups = GroupTracker.Instance;
            var n = groups?.NewcomerOf(newcomer);
            if (n != null)
            {
                var g = groups.Get(n.groupId);
                bool member = g != null && g.Has(host);
                n.welcomes += WelcomeMath.Welcomes(member);
                if (member && n.sponsor == null && ArcUtil.Opinion(host, newcomer) > 24) n.sponsor = host;
            }
            var arc = ArcTracker.Instance?.Of(newcomer);
            var talk = arc?.Gate?.talk;
            if (arc?.def?.Worker is ArcWorker_Newcomer && arc.other == host && talk != null) ArcTracker.Instance.Notify_Talked(talk, newcomer, host);
        }
    }

    /// <summary>What colony life's play-outs leave when they are over (from PlayAfter.Run).</summary>
    public static class LifePlays
    {
        public const string Prefix = "life:";

        public static void Over(PlayOut p, string what, bool happened, List<Pawn> took)
        {
            try
            {
                if (what == DilemmaEffect_WelcomeEvening.After) DilemmaEffect_WelcomeEvening.Over(p, happened);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("colony life play over " + what, e);
            }
        }
    }
}
