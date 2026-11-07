using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>One pair at the evening's drill: who, where, how far along, and the one real blow if it comes.</summary>
    public sealed class DrillBout
    {
        public Pawn a, b;
        public Map map;
        /// <summary>The first of the pair's place; the other stands beside it.</summary>
        public IntVec3 ring;
        public int start;
        public int swings;
        /// <summary>The swing on which the real blow lands (-1: none this bout); even swings are a's at b, odd b's at a.</summary>
        public int hardAt = -1;
        /// <summary>Who the real blow fell on, once it did.</summary>
        public Pawn hurt;
        public bool over;
        /// <summary>The militia's muster rather than the drills decree.</summary>
        public bool muster;
        /// <summary>A games day's bout (Harm/GamesDay.cs) rather than a drill: its words, its form by the age, its kind.</summary>
        public bool games;

        public bool Has(Pawn p) => p != null && (p == a || p == b);

        public Pawn Other(Pawn p) => p == a ? b : a;
    }

    /// <summary>
    /// Drills that bruise (the harm review's 1.2.1): with "Answers play out on the map" on, the drills decree pairs
    /// everyone who can fight into practice bouts in the open each evening (five to eight), and the militia's muster one
    /// pair most evenings (three in four). Each bout is six swings each, pulled blows that only show; one bout in 25 lands
    /// one for real (a marshal in office halves it), a bruise or a cut of 6 to 9 by the colony's means (sticks and
    /// wrestling, blunted blades, the bayonet's graze, a stun baton's burn), on one of the pair who may be hurt (Harm's
    /// cap). The hurt one says so, the other says sorry, the news tells it, and the game's own doctor tends it. Children
    /// never spar; the decree's hourly practice goes on as before. The bouts aren't saved: a reload ends them.
    /// </summary>
    public static class DrillBouts
    {
        private static readonly List<DrillBout> bouts = new List<DrillBout>();
        private static JobDef job;

        public static JobDef Job => job ??= DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_DrillBout");

        public static void Reset()
        {
            bouts.Clear();
            job = null;
        }

        public static DrillBout Of(Pawn p)
        {
            for (int i = 0; i < bouts.Count; i++)
                if (bouts[i].Has(p) && !bouts[i].over) return bouts[i];
            return null;
        }

        private static int Now => Find.TickManager.TicksGame;

        // ================= The evening's call =================

        /// <summary>Once an hour: at each home whose colony drills, in the evening, the pairs are called once a day.</summary>
        public static void Hourly(HarmBook book, int now)
        {
            bouts.RemoveAll(x => x.over || now - x.start > 2 * GenDate.TicksPerHour);
            if (!PlayOuts.On || Job == null) return;
            var all = CouncilGameComponent.Instance?.colonies;
            if (all == null) return;
            foreach (var c in all)
            {
                if (c == null || c.lostAt != ColonyStateMath.NotLost) continue;
                var d = c.decrees;
                DecreeDef def = null;
                foreach (var a in d.active)
                    if (a?.def != null && (a.def.hurts == DecreeHurt.Drills || (a.def.hurts == DecreeHurt.Muster && def == null)))
                        def = a.def;
                if (def == null) continue;
                var map = d.Ground;
                if (map == null || !HarmMath.DrillHour(GenLocalDate.HourOfDay(map))) continue;
                int day = GenLocalDate.Year(map) * 60 + GenLocalDate.DayOfYear(map);
                if (book.DrilledToday(map, day)) continue;
                if (PlayOuts.WhyNotNow(map, false) != null) continue;
                bool muster = def.hurts == DecreeHurt.Muster;
                if (muster && !HarmMath.MusterEvening(day))
                {
                    book.Drilled(map, day);
                    continue;
                }
                // Bad weather, or nobody free yet: the next hour of the evening tries again.
                if (!JoyUtility.EnjoyableOutsideNow(map)) continue;
                try
                {
                    if (Call(map, d, muster) > 0) book.Drilled(map, day);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("harm: the evening drill", e);
                }
            }
        }

        /// <summary>Someone of the colony free for an evening's bout: a grown-up who can fight and use their hands, free and not pressed.</summary>
        private static bool CanDrill(Pawn p, DecreeTracker d) =>
            PlayOuts.OurPerson(p) && d.Ours(p) && !p.DevelopmentalStage.Juvenile() && !p.WorkTagIsDisabled(WorkTags.Violent) && Together.Able(p)
            && PlayOuts.CanJoin(p) && PlayOuts.Interruptible(p) && !PlayOuts.NeedsPress(p) && p.GetLord() == null && Of(p) == null
            && !(p.skills?.GetSkill(SkillDefOf.Melee)?.TotallyDisabled ?? true);

        /// <summary>The evening's pairs called to their places; how many bouts began.</summary>
        private static int Call(Map map, DecreeTracker d, bool muster)
        {
            var fighters = map.mapPawns.FreeColonistsSpawned.Where(p => CanDrill(p, d)).InRandomOrder().ToList();
            int pairs = HarmMath.DrillPairs(fighters.Count, muster);
            if (pairs == 0) return 0;
            var center = PlaySpots.OpenCell(map, fighters[0]);
            if (!center.IsValid) return 0;
            bool marshal = (ColonyState.For(map)?.offices?.DrillFactor ?? 1f) > 1f;
            var taken = new List<IntVec3>();
            Pawn first = null;
            int started = 0;
            for (int i = 0; i < pairs; i++)
            {
                var a = fighters[2 * i];
                var b = fighters[2 * i + 1];
                if (!Places(map, center, a, b, taken, out var ra, out var rb)) continue;
                if (!Begin(map, a, b, ra, rb, marshal, muster)) continue;
                started++;
                first ??= a;
            }
            // apart: the call goes out to everyone drilling; the pairs meet at their places.
            if (first != null) Say(first, "Rimfolk_Pool_DrillCall");
            return started;
        }

        /// <summary>Two places side by side in the open near the yard, clear of the other pairs.</summary>
        internal static bool Places(Map map, IntVec3 center, Pawn a, Pawn b, List<IntVec3> taken, out IntVec3 ra, out IntVec3 rb)
        {
            ra = rb = IntVec3.Invalid;
            foreach (var c in GenRadial.RadialCellsAround(center, 9f, true))
            {
                if (!Clear(c, map, a) || taken.Any(t => t.InHorDistOf(c, 3f))) continue;
                foreach (var n in GenAdj.CellsAdjacentCardinal(c, Rot4.North, IntVec2.One))
                {
                    if (!Clear(n, map, b)) continue;
                    ra = c;
                    rb = n;
                    taken.Add(c);
                    return true;
                }
            }
            return false;
        }

        private static bool Clear(IntVec3 c, Map map, Pawn p) =>
            c.InBounds(map) && c.GetEdifice(map) == null && c.GetFirstPawn(map) == null && !(c.GetTerrain(map)?.IsWater ?? false) && PlaySpots.Ok(c, map, p);

        internal static bool Begin(Map map, Pawn a, Pawn b, IntVec3 ra, IntVec3 rb, bool marshal, bool muster, bool games = false)
        {
            var bout = new DrillBout { a = a, b = b, map = map, ring = ra, start = Now, muster = muster, games = games };
            // The one real blow, decided at the start: one bout in 25 (half with a marshal), on one of them who may take it.
            if (games ? Harm.Roll(HarmKinds.Games, HarmFourMath.GamesBlowChance) : Harm.Roll(HarmKinds.Drills, HarmMath.DrillBlow(marshal)))
            {
                var can = new List<Pawn>();
                if (Harm.May(a, HurtClass.Bout)) can.Add(a);
                if (Harm.May(b, HurtClass.Bout)) can.Add(b);
                if (can.Count > 0)
                {
                    var who = can.RandomElement();
                    int k = Rand.RangeInclusive(1, HarmMath.DrillSwings - 1);
                    bout.hardAt = who == b ? 2 * k : 2 * k + 1;
                }
            }
            bouts.Add(bout);
            var ja = JobMaker.MakeJob(Job, b, ra);
            var jb = JobMaker.MakeJob(Job, a, rb);
            a.jobs.StartJob(ja, JobCondition.InterruptForced, null, false, true);
            b.jobs.StartJob(jb, JobCondition.InterruptForced, null, false, true);
            if (a.CurJobDef == Job && b.CurJobDef == Job) return true;
            bouts.Remove(bout);
            if (a.CurJobDef == Job) a.jobs.EndCurrentJob(JobCondition.InterruptForced);
            if (b.CurJobDef == Job) b.jobs.EndCurrentJob(JobCondition.InterruptForced);
            return false;
        }

        // ================= The bout =================

        /// <summary>A swing (from the first of the pair's job): the lunge and the sound, no harm, except the one real blow.</summary>
        public static void Swing(DrillBout bout)
        {
            if (bout == null || bout.over) return;
            try
            {
                var attacker = bout.swings % 2 == 0 ? bout.a : bout.b;
                var defender = bout.Other(attacker);
                if (attacker?.Map == null || defender == null) return;
                attacker.Drawer?.Notify_MeleeAttackOn(defender);
                bool hard = bout.swings == bout.hardAt;
                bout.swings++;
                if (!hard)
                {
                    (Rand.Chance(0.5f) ? SoundDefOf.Pawn_Melee_Punch_Miss : SoundDefOf.Pawn_Melee_Punch_HitPawn)?.PlayOneShot(new TargetInfo(defender.Position, defender.Map));
                    if (bout.swings >= HarmMath.DrillSwings * 2) End(bout, true);
                    return;
                }
                RealBlow(bout, attacker, defender);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("harm: a drill swing", e);
            }
        }

        /// <summary>The real blow, by the colony's means: a bruise from a stick or a blunted blade, a bayonet's graze, a baton's burn.</summary>
        public static void BlowOf(WorldAge age, out HurtKind kind, out HurtWhere where)
        {
            switch (age)
            {
                case WorldAge.Tribal:
                    kind = HurtKind.Bruise;
                    where = HurtWhere.Any;
                    return;
                case WorldAge.Medieval:
                    kind = Rand.Chance(0.5f) ? HurtKind.Cut : HurtKind.Bruise;
                    where = HurtWhere.Arm;
                    return;
                case WorldAge.Industrial:
                    kind = Rand.Chance(0.5f) ? HurtKind.Cut : HurtKind.Bruise;
                    where = HurtWhere.Arm;
                    return;
                default:
                    kind = HurtKind.Burn;
                    where = HurtWhere.Torso;
                    return;
            }
        }

        /// <summary>A games day's real blow, by the colony's means: a wrestler's bruise, a stave's on the arm, a boxer's, a sim tourney's wrenched wrist.</summary>
        public static void GamesBlowOf(WorldAge age, out HurtKind kind, out HurtWhere where)
        {
            kind = HurtKind.Bruise;
            switch (age)
            {
                case WorldAge.Tribal: where = HurtWhere.Any; return;
                case WorldAge.Medieval: where = HurtWhere.Arm; return;
                case WorldAge.Industrial: where = Rand.Bool ? HurtWhere.Torso : HurtWhere.Arm; return;
                default: where = HurtWhere.Hand; return;
            }
        }

        private static void RealBlow(DrillBout bout, Pawn attacker, Pawn defender)
        {
            var age = Ages.Ours;
            HurtKind kind;
            HurtWhere where;
            if (bout.games) GamesBlowOf(age, out kind, out where);
            else BlowOf(age, out kind, out where);
            bool wrist = bout.games && age == WorldAge.Spacer;
            var h = Harm.Injure(defender, kind, where, wrist ? HarmFourMath.WristLow : HarmMath.DrillLow, wrist ? HarmFourMath.WristHigh : HarmMath.DrillHigh,
                bout.games ? HarmKinds.Games : HarmKinds.Drills, HurtClass.Bout);
            if (h == null)
            {
                SoundDefOf.Pawn_Melee_Punch_HitPawn?.PlayOneShot(new TargetInfo(defender.Position, defender.Map));
                return;
            }
            SoundDefOf.Pawn_Melee_Punch_HitPawn?.PlayOneShot(new TargetInfo(defender.Position, defender.Map));
            bout.hurt = defender;
            if (bout.games)
            {
                // together: the two are at their bout, side by side; a games day's hurt is seen there, with no message.
                Say(defender, "Rimfolk_Pool_GamesHit", attacker, wrist ? "wrist" : kind.ToString().ToLowerInvariant());
                Say(attacker, "Rimfolk_Pool_GamesSorry", defender);
                HarmFour.Mote(defender, wrist ? "CC_HarmFour_Mote_Wrist" : "CC_HarmFour_Mote_GamesBlow");
                End(bout, false);
                return;
            }
            // together: the two are at their bout, side by side; each says their word to the other.
            Say(defender, "Rimfolk_Pool_DrillHit", attacker, kind.ToString().ToLowerInvariant());
            Say(attacker, "Rimfolk_Pool_DrillSorry", defender);
            string text = TextVariants.Vary(bout.muster ? "CC_Harm_MusterBlow" : "CC_Harm_DrillBlow")
                .Translate(defender.Named("PAWN"), attacker.Named("OTHER"), (h.Part?.Label ?? "").Named("PART")).Resolve();
            Tell.Message(NewsKind.Health, Importance.Important, defender.Map, text, new LookTargets(defender), MessageTypeDefOf.NegativeHealthEvent);
            End(bout, false);
        }

        /// <summary>The bout ends: done (a word from one of them), or cut short (either left it, or the blow fell).</summary>
        public static void End(DrillBout bout, bool done)
        {
            if (bout == null || bout.over) return;
            bout.over = true;
            if (!done || bout.hurt != null) return;
            var a = bout.a;
            var b = bout.b;
            // together: the pair finish side by side at their bout.
            if (a == null || b == null || !Together.Can(a, b)) return;
            var speaker = Rand.Chance(0.5f) ? a : b;
            Say(speaker, bout.games ? "Rimfolk_Pool_GamesEnd" : "Rimfolk_Pool_DrillEnd", bout.Other(speaker));
        }

        private static readonly Color SayColor = new Color(0.95f, 0.85f, 0.7f);

        private static void Say(Pawn p, string pool, Pawn to = null, string tag = null)
        {
            try
            {
                if (to == p) to = null;
                string line = Flavor.FromPool(pool, p, null, null, tag);
                if (line.NullOrEmpty()) return;
                // together: a line to the other of the pair, said only while the two stand together at their bout.
                var args = to != null ? new[] { p.Named("PAWN"), to.Named("OTHER") } : new[] { p.Named("PAWN") };
                Speech.Say(p, line.Formatted(args).Resolve(), to, SayColor);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("harm: a drill line from " + pool, e);
            }
        }
    }

    /// <summary>
    /// One of a pair at the evening's drill (DrillBouts): to their place (B) beside the other (A), a word and a nod, then
    /// six swings each, pulled, the first of the pair keeping count; a few moments after. Either one leaving ends it for
    /// both; danger ends it at once.
    /// </summary>
    public class JobDriver_DrillBout : JobDriver
    {
        private int swingIn = 60, waited;

        private Pawn Partner => job.targetA.Thing as Pawn;

        private DrillBout Bout => DrillBouts.Of(pawn);

        private bool Lead => Bout != null && Bout.a == pawn;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        public override string GetReport() =>
            Partner != null ? ((Bout?.games ?? false) ? "CC_HarmFour_Report_Games" : "CC_Harm_Report_Drill").Translate(Partner.Named("OTHER")).Resolve() : base.GetReport();

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref swingIn, "swingIn", 60);
            Scribe_Values.Look(ref waited, "waited");
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() => Bout == null && CurToilIndex < 3);
            this.FailOn(() => pawn.Map == null || pawn.Map.dangerWatcher.DangerRating != StoryDanger.None);
            AddFinishAction(cond =>
            {
                var b = DrillBouts.Of(pawn);
                if (b != null) DrillBouts.End(b, false);
            });
            yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);
            var wait = ToilMaker.MakeToil("DrillBoutWait");
            wait.defaultCompleteMode = ToilCompleteMode.Never;
            wait.handlingFacing = true;
            wait.tickIntervalAction = delta =>
            {
                var o = Partner;
                if (o == null) return;
                pawn.rotationTracker.FaceTarget(o);
                waited += delta;
                if (o.Position.InHorDistOf(pawn.Position, 1.5f) && o.CurJobDef == DrillBouts.Job && !o.pather.Moving) ReadyForNextToil();
                else if (waited > 2000) EndJobWith(JobCondition.Incompletable);
            };
            yield return wait;
            var salute = ToilMaker.MakeToil("DrillBoutSalute");
            salute.defaultCompleteMode = ToilCompleteMode.Delay;
            salute.defaultDuration = 120;
            salute.handlingFacing = true;
            salute.initAction = () =>
            {
                // together: the two face each other at their places.
                if (Lead && Partner != null) DrillWords.Begin(pawn, Partner, Bout?.games ?? false);
            };
            salute.tickIntervalAction = delta =>
            {
                if (Partner != null) pawn.rotationTracker.FaceTarget(Partner);
            };
            yield return salute;
            var fight = ToilMaker.MakeToil("DrillBoutFight");
            fight.defaultCompleteMode = ToilCompleteMode.Never;
            fight.handlingFacing = true;
            fight.socialMode = RandomSocialMode.Off;
            fight.tickIntervalAction = delta =>
            {
                var bout = Bout;
                var o = Partner;
                if (bout == null || o == null || bout.over)
                {
                    ReadyForNextToil();
                    return;
                }
                pawn.rotationTracker.FaceTarget(o);
                if (!Lead) return;
                swingIn -= delta;
                if (swingIn > 0) return;
                swingIn = HarmMath.DrillSwingTicks + Rand.Range(0, 40);
                DrillBouts.Swing(bout);
                if (bout.over) ReadyForNextToil();
            };
            yield return fight;
            var after = ToilMaker.MakeToil("DrillBoutAfter");
            after.defaultCompleteMode = ToilCompleteMode.Delay;
            after.defaultDuration = 120;
            after.handlingFacing = true;
            after.tickIntervalAction = delta =>
            {
                var o = Partner;
                if (o != null && o.Spawned) pawn.rotationTracker.FaceTarget(o);
            };
            yield return after;
        }
    }

    /// <summary>The words at a bout's start, by the colony's means (the pools' lines are tagged by age).</summary>
    public static class DrillWords
    {
        public static void Begin(Pawn a, Pawn b, bool games = false)
        {
            try
            {
                string line = Flavor.FromPool(games ? "Rimfolk_Pool_GamesBegin" : "Rimfolk_Pool_DrillBegin", a);
                if (line.NullOrEmpty()) return;
                // together: the pair face each other at their places when the first of them says it.
                Speech.Say(a, line.Formatted(a.Named("PAWN"), b.Named("OTHER")).Resolve(), b, new Color(0.95f, 0.85f, 0.7f));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("harm: a drill's first word", e);
            }
        }
    }
}
