using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>A wedding the council planned with the couple (CouncilGameComponent.weddingPlans): how, and from when to when it may be held.</summary>
    public class WeddingPlan : IExposable
    {
        public Pawn a, b;
        /// <summary>"feast", "quiet" or "wait" (WeddingPlans).</summary>
        public string style;
        public int from, until;

        public void ExposeData()
        {
            Scribe_References.Look(ref a, "a");
            Scribe_References.Look(ref b, "b");
            Scribe_Values.Look(ref style, "style");
            Scribe_Values.Look(ref from, "from");
            Scribe_Values.Look(ref until, "until");
        }
    }

    /// <summary>
    /// An engaged couple plans the wedding with the council (Council_DlmFam_WeddingPlans). The answer sets how it's held:
    /// the game's own ceremony on the next fine evening, with a feast for the whole colony after it, or with a toast
    /// among their closest friends; or the council asks them to wait for a calmer time, and the wedding is held then.
    /// The ceremony is started when the evening comes and both are ready (as the game itself would, tried each hour
    /// from three in the afternoon until nine); what follows it begins once they're married (Patch_Married). Not held
    /// within a few days of its time, the plan lapses and the game marries them in its own time, as before.
    /// </summary>
    public static class WeddingPlans
    {
        public const string Feast = "feast", Quiet = "quiet", Wait = "wait";

        private static List<WeddingPlan> List => CouncilGameComponent.Instance?.weddingPlans;

        private static bool Pair(WeddingPlan w, Pawn x, Pawn y) => w != null && (w.a == x && w.b == y || w.a == y && w.b == x);

        public static bool Planned(Pawn x, Pawn y) => List?.Any(w => Pair(w, x, y)) ?? false;

        /// <summary>The wedding set: from the next afternoon (or, waiting, in a fortnight or so), for a few days.</summary>
        public static void Plan(Pawn a, Pawn b, string style)
        {
            var list = List;
            if (list == null || a == null || b == null) return;
            list.RemoveAll(w => Pair(w, a, b));
            int now = Find.TickManager.TicksGame;
            int from = style == Wait ? now + Rand.Range(10, 15) * GenDate.TicksPerDay : now + GenDate.TicksPerHour * 2;
            list.Add(new WeddingPlan { a = a, b = b, style = style, from = from, until = from + GenDate.TicksPerDay * 5 });
        }

        /// <summary>Each hour (Social/Family.cs): a planned wedding's evening has come, and both are ready: the game's own ceremony begins.</summary>
        public static void Hourly(CouncilGameComponent comp)
        {
            // apart: a plan that lapsed is the council's note; the game marries them in its own time, as before.
            var list = comp?.weddingPlans;
            if (list == null || list.Count == 0) return;
            int now = Find.TickManager.TicksGame;
            foreach (var w in list.ToList())
            {
                var a = w.a;
                var b = w.b;
                if (a == null || b == null || a.Dead || b.Dead || a.relations == null || !a.relations.DirectRelationExists(PawnRelationDefOf.Fiance, b))
                {
                    list.Remove(w);
                    continue;
                }
                if (now > w.until)
                {
                    list.Remove(w);
                    if (a.Spawned) Messages.Message("CC_Wed_Lapsed".Translate(a.Named("A"), b.Named("B")).Resolve(), new LookTargets(a), MessageTypeDefOf.NeutralEvent, false);
                    continue;
                }
                if (now < w.from || !a.Spawned || !b.Spawned || a.Map != b.Map || !a.Map.IsPlayerHome) continue;
                int hour = GenLocalDate.HourOfDay(a.Map);
                if (hour < 15 || hour > 20 || a.GetLord() != null || b.GetLord() != null || a.Drafted || b.Drafted) continue;
                try
                {
                    var map = a.Map;
                    if (map.lordsStarter == null || !MarriageCeremonyUtility.AcceptableGameConditionsToStartCeremony(map)
                        || !MarriageCeremonyUtility.FianceReadyToStartCeremony(a, b) || !MarriageCeremonyUtility.FianceReadyToStartCeremony(b, a))
                        continue;
                    map.lordsStarter.TryStartMarriageCeremony(a, b);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("a planned wedding", e);
                    list.Remove(w);
                }
            }
        }

        /// <summary>They're married (the game's own ceremony, whoever started it): what the council planned to follow it begins.</summary>
        public static void Married(Pawn x, Pawn y)
        {
            var list = List;
            var w = list?.FirstOrDefault(p => Pair(p, x, y));
            if (w == null) return;
            list.Remove(w);
            var map = x?.MapHeld;
            if (map == null || !PlayOuts.On) return;
            try
            {
                if (w.style == Feast)
                {
                    new DilemmaEffect_Feast
                    {
                        target = DilemmaTarget.Colony, hours = new IntRange(0, 23), waitHours = 8f, duration = new FloatRange(2f, 3f),
                        label = "CC_Wed_FeastLabel".Translate(x.Named("A"), y.Named("B")).Resolve()
                    }.Begin(new DilemmaContext { map = map, a = x, b = y });
                }
                else if (w.style == Quiet)
                {
                    var friend = map.mapPawns.FreeColonistsSpawned.Where(o => o != x && o != y && !o.IsQuestLodger() && o.relations != null)
                        .OrderByDescending(o => o.relations.OpinionOf(x) + o.relations.OpinionOf(y)).FirstOrDefault();
                    new DilemmaEffect_Toast
                    {
                        target = DilemmaTarget.CloseToA, organizer = friend != null ? PlayWho.B : PlayWho.A, hours = new IntRange(0, 23), waitHours = 6f,
                        label = "CC_Wed_ToastLabel".Translate(x.Named("A"), y.Named("B")).Resolve(),
                        lines = new List<string> { "CC_Wed_Toast_1".Translate(x.Named("A"), y.Named("B")).Resolve(), "CC_Wed_Toast_2".Translate(x.Named("A"), y.Named("B")).Resolve() }
                    }.Begin(new DilemmaContext { map = map, a = x, b = friend ?? y });
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("after a planned wedding", e);
            }
        }
    }

    /// <summary>
    /// An engaged couple at home, engaged a couple of days or more, asked about once (CouncilGameComponent.weddingAsked),
    /// with no wedding under way or planned: A and B, the two of them.
    /// </summary>
    public class DilemmaWorker_WeddingPlans : DilemmaWorker
    {
        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            var comp = CouncilGameComponent.Instance;
            if (comp == null || !CanHappenNow(map)) return false;
            if (map.lordManager.lords.Any(l => l.LordJob is LordJob_Joinable_MarriageCeremony)) return false;
            int now = Find.TickManager.TicksGame;
            foreach (var a in map.mapPawns.FreeColonistsSpawned.Where(p => !p.IsQuestLodger() && p.DevelopmentalStage.Adult() && p.relations != null).InRandomOrder())
            {
                foreach (var r in a.relations.DirectRelations.Where(r => r.def == PawnRelationDefOf.Fiance).ToList())
                {
                    var b = r.otherPawn;
                    if (b == null || b.Dead || !b.IsFreeColonist || !b.Spawned || b.Map != map || b.IsQuestLodger()) continue;
                    if (now - r.startTicks < GenDate.TicksPerDay * 2 || WeddingPlans.Planned(a, b)) continue;
                    string key = WishWorker_ShareBed.PairKey(a, b);
                    if (comp.weddingAsked.Contains(key)) continue;
                    comp.weddingAsked.Add(key);
                    if (comp.weddingAsked.Count > 40) comp.weddingAsked.RemoveRange(0, comp.weddingAsked.Count - 40);
                    ctx = new DilemmaContext { def = def, map = map, a = a, b = b, count = def.thingCount };
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>The council sets how the couple (A and B) are married (<see cref="style"/>: WeddingPlans' feast, quiet or wait).</summary>
    public class DilemmaEffect_PlanWedding : DilemmaEffect
    {
        public string style = WeddingPlans.Quiet;

        public override void Apply(DilemmaContext ctx)
        {
            // apart: the council's word on the day; the wedding itself plays out on the map when it comes.
            if (ctx?.a == null || ctx.b == null || ctx.a.Dead || ctx.b.Dead) return;
            WeddingPlans.Plan(ctx.a, ctx.b, style);
        }
    }
}
