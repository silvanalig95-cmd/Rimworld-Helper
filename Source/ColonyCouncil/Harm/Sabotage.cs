using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// A rival's sabotage, found out (the harm review's 3.5.1). In a bitter rivalry (the social web's rivals at 50 or
    /// more), one of a petty or abrasive nature (abrasive or jealous, never kind) may do the other's work a bad turn: about
    /// twice a 60-day year while their work is shared, half that with it kept apart, half again with the peacemaker in
    /// office, times the harm scale. The other's work is a bench (the colony's best of them at crafting, art or cooking, its
    /// bench with the most bills of that work); by day, with the other well away from it, the rival walks there and is at
    /// it a few seconds (the job Rimfolk_Sabotage): a machine is jammed (the game's breakdown, a component to mend it) in
    /// an industrial or spacer colony, else the bench is damaged (a quarter of its hit points, never below a third, the
    /// game's repair puts it right). The words are by the colony's means: a charm taken, a tool broken, a machine jammed, a
    /// program tampered with. Found out half the time: a witness who saw it (within 12 cells, a clear line) tells the
    /// other, or with none, word goes round within the day. Found out, the other goes to have it out with them, and it is
    /// the game's own social fight under the harm cap for both (words only when the cap holds); the rivalry deepens. Not
    /// found out, the bench's state is told and nobody is named. Needs the social web and answers on the map; nothing is
    /// saved but the bench's state (a load forgets an intent or a discovery under way).
    /// </summary>
    public static class Sabotage
    {
        public static bool On => Harm.On && PlayOuts.On && (ColonyCouncilMod.Settings?.socialWeb ?? true);

        private sealed class Intent
        {
            public Pawn rival, other;
            public Building_WorkTable bench;
            public int until;
        }

        private static readonly List<Intent> intents = new List<Intent>();
        /// <summary>Each rivalry's last day rolled, by the pair's ids (a day's one chance).</summary>
        private static readonly Dictionary<long, int> rolled = new Dictionary<long, int>();
        private static JobDef job;

        public static JobDef Job => job ??= DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_Sabotage");

        public static void Reset()
        {
            intents.Clear();
            rolled.Clear();
            job = null;
        }

        private static int Now => Find.TickManager.TicksGame;

        private static readonly string[] Crafts = { "Crafting", "Artistic", "Cooking" };

        /// <summary>Petty or abrasive: abrasive or jealous, and not kind.</summary>
        public static bool Petty(Pawn p)
        {
            var t = p?.story?.traits;
            if (t == null || t.HasTrait(TraitDefOf.Kind)) return false;
            if (t.HasTrait(TraitDefOf.Abrasive)) return true;
            var jealous = DefDatabase<TraitDef>.GetNamedSilentFail("Jealous");
            return jealous != null && t.HasTrait(jealous);
        }

        /// <summary>Someone's craft: the skill among crafting, art and cooking they're best at (6 or more, the work theirs to do), or null.</summary>
        public static SkillDef CraftOf(Pawn p)
        {
            if (p?.skills == null) return null;
            SkillDef best = null;
            int level = 5;
            foreach (var n in Crafts)
            {
                var s = DefDatabase<SkillDef>.GetNamedSilentFail(n);
                var r = s == null ? null : p.skills.GetSkill(s);
                if (r == null || r.TotallyDisabled || r.Level <= level) continue;
                best = s;
                level = r.Level;
            }
            return best;
        }

        /// <summary>The bench of that craft with the most bills of it, on their map.</summary>
        public static Building_WorkTable BenchOf(Pawn p, SkillDef craft)
        {
            if (p?.Map == null || craft == null) return null;
            Building_WorkTable best = null;
            int most = 0;
            foreach (var b in p.Map.listerBuildings.allBuildingsColonist)
            {
                if (!(b is Building_WorkTable t) || t.BillStack == null || !t.Spawned || t.IsBurning()) continue;
                int n = t.BillStack.Bills.Count(bill => bill?.recipe?.workSkill == craft && !bill.suspended);
                if (n > most)
                {
                    best = t;
                    most = n;
                }
            }
            return best;
        }

        private static long Key(Pawn a, Pawn b) => ((long)a.thingIDNumber << 32) | (uint)b.thingIDNumber;

        /// <summary>The hour's look (HarmBook): each bitter rivalry's day's chance, and an intent waiting for its hour.</summary>
        public static void Hourly(HarmBook book, int now)
        {
            if (!On || Job == null) return;
            try
            {
                intents.RemoveAll(i => i.until < now || i.rival == null || i.rival.Dead || i.other == null || i.other.Dead || i.bench == null || !i.bench.Spawned);
                Roll(now);
                foreach (var i in intents.ToList()) TryAct(i);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("harm: a rival's sabotage", e);
            }
        }

        private static void Roll(int now)
        {
            var web = CouncilGameComponent.Instance?.social;
            if (web == null) return;
            int day = now / GenDate.TicksPerDay;
            foreach (var t in web.ties)
            {
                if (t == null || t.kind != TieKind.Rivals || t.strength < HarmTwoMath.BitterRivalry) continue;
                foreach (var (rival, other) in new[] { (t.a, t.b), (t.b, t.a) })
                {
                    if (!Fit(rival) || !Fit(other) || rival.Map != other.Map || intents.Any(i => i.rival == rival)) continue;
                    long key = Key(rival, other);
                    if (rolled.TryGetValue(key, out int d) && d == day) continue;
                    rolled[key] = day;
                    var craft = CraftOf(other);
                    var bench = BenchOf(other, craft);
                    if (bench == null) continue;
                    bool same = CraftOf(rival) == craft;
                    var pm = ColonyState.For(rival.Map)?.offices?.Peacemaker;
                    float chance = HarmTwoMath.SabotageChance(t.strength, Petty(rival), same, pm != null);
                    if (!Harm.Roll(HarmKinds.Sabotage, chance)) continue;
                    intents.Add(new Intent { rival = rival, other = other, bench = bench, until = now + (int)(HarmTwoMath.IntentDays * GenDate.TicksPerDay) });
                }
            }
            if (rolled.Count > 400) rolled.Clear();
        }

        /// <summary>Dev mode (Harm/HarmTwoDebug.cs): the rival means to do it today, whatever the rivalry's strength and the roll.</summary>
        internal static string DebugIntent(Pawn rival, Pawn other)
        {
            if (!On || Job == null) return "needs hurts from everyday life, answers on the map and the social web.";
            if (!Fit(rival) || !Fit(other) || rival.Map != other.Map) return "both need to be up and about on the same map.";
            var bench = BenchOf(other, CraftOf(other));
            if (bench == null) return other.LabelShort + " has no craft of 6 or more with a bench of bills for it.";
            intents.RemoveAll(i => i.rival == rival);
            intents.Add(new Intent { rival = rival, other = other, bench = bench, until = Now + (int)(HarmTwoMath.IntentDays * GenDate.TicksPerDay) });
            return rival.LabelShort + " means to go to " + other.LabelShort + "'s " + bench.LabelShort + " today, by day, once " + other.LabelShort + " is well away from it.";
        }

        private static bool Fit(Pawn p) =>
            p != null && p.Spawned && !p.Dead && PlayOuts.OurPerson(p) && p.IsColonist && !p.DevelopmentalStage.Juvenile() && Together.Able(p);

        /// <summary>The hour for it: by day, the bench free, the other well away, the rival free and not pressed, no danger.</summary>
        private static void TryAct(Intent i)
        {
            var r = i.rival;
            var map = r.Map;
            if (map == null || i.bench.Map != map || !HarmTwoMath.SabotageHour(GenLocalDate.HourOfDay(map)) || PlayOuts.WhyNotNow(map, false) != null) return;
            if (!PlayOuts.CanJoin(r) || !PlayOuts.Interruptible(r) || PlayOuts.NeedsPress(r) || r.CurJobDef == Job) return;
            if (i.other.Spawned && i.other.Map == map && i.other.Position.InHorDistOf(i.bench.Position, HarmTwoMath.AwayFromBench)) return;
            var cell = i.bench.InteractionCell;
            if (!cell.IsValid || cell.GetFirstPawn(map) != null || !r.CanReach(i.bench, PathEndMode.InteractionCell, Danger.Some)) return;
            var j = JobMaker.MakeJob(Job, i.bench);
            r.jobs.StartJob(j, JobCondition.InterruptForced, null, false, true);
        }

        /// <summary>The rival finished at the bench (the job's last toil): the bench's harm, who saw, and whether it's found out.</summary>
        public static void Done(Pawn rival, Building_WorkTable bench)
        {
            var i = intents.FirstOrDefault(x => x.rival == rival && x.bench == bench);
            if (i == null || bench == null || !bench.Spawned) return;
            intents.Remove(i);
            try
            {
                var map = bench.Map;
                var age = Ages.Ours;
                var breakdown = bench.TryGetComp<CompBreakdownable>();
                bool jammed = age >= WorldAge.Industrial && breakdown != null && !breakdown.BrokenDown;
                if (jammed) breakdown.DoBreakdown();
                else
                {
                    bench.HitPoints = HarmTwoMath.BenchHitPoints(bench.HitPoints, bench.MaxHitPoints);
                    // The game's builders see it needs mending, as after any damage.
                    map.listerBuildingsRepairable.Notify_BuildingTookDamage(bench);
                    FleckMaker.ThrowDustPuffThick(bench.TrueCenter(), map, 1.2f, new Color(0.6f, 0.55f, 0.5f));
                }
                // The words say what was done: a machine jammed only where there was one, the bench knocked about otherwise.
                string form = HarmTwoMath.SabotageForm(age, jammed);
                string what = ("CC_HarmTwo_Sabotage_What_" + form).Translate(i.other.Named("OTHER"), bench.LabelShort.Named("BENCH")).Resolve();
                var witness = map.mapPawns.FreeColonistsSpawned
                    .Where(w => w != rival && w != i.other && Together.Able(w) && w.Position.InHorDistOf(bench.Position, HarmTwoMath.WitnessRange)
                                && GenSight.LineOfSight(w.Position, bench.Position, map))
                    .OrderBy(w => w.Position.DistanceToSquared(bench.Position)).FirstOrDefault();
                // Someone who saw it always tells; with nobody, word goes round half the time.
                if (!Rand.Chance(HarmTwoMath.FoundOut(witness != null)))
                {
                    // Nobody tells: the bench's state is the news, and nobody is named for it.
                    Tell.Message(NewsKind.Colony, Importance.Notable, map, TextVariants.Vary("CC_HarmTwo_SabotageUnknown").Translate(what.Named("WHAT"), i.other.Named("OTHER")).Resolve(),
                        new LookTargets(bench), MessageTypeDefOf.NegativeEvent);
                    return;
                }
                string key = "sabotage|" + rival.thingIDNumber + "|" + Now;
                if (witness != null)
                {
                    // together: the witness, near the bench, says it to the rival when they're within talking range.
                    if (Together.Can(witness, rival)) Say(witness, "Rimfolk_Pool_SabotageSeen", rival);
                    Together.When(key, witness, i.other, () =>
                    {
                        // together: the witness tells the other face to face (Together.When).
                        Say(witness, "Rimfolk_Pool_SabotageTold", i.other, rival);
                        Confront(i.other, rival, what, witness, form);
                    }, HarmTwoMath.GossipHours, 0f);
                    return;
                }
                Together.WhenInCompany(key, i.other, with =>
                {
                    // together: whoever is with the other says what's being said (Together.WhenInCompany).
                    Say(with, "Rimfolk_Pool_SabotageWord", i.other, rival);
                    Confront(i.other, rival, what, null, form);
                }, HarmTwoMath.GossipHours, rival);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("harm: a sabotage done", e);
            }
        }

        /// <summary>The other knows now: they go to have it out with the rival, and it comes to blows (the cap for both), or to words.</summary>
        private static void Confront(Pawn other, Pawn rival, string what, Pawn witness, string form)
        {
            if (other == null || rival == null || other.Dead || rival.Dead) return;
            Together.When("sabotage confront|" + other.thingIDNumber + "|" + rival.thingIDNumber, other, rival, () =>
            {
                try
                {
                    // together: the two face to face (Together.When); the words name what was done to the bench.
                    Say(other, "Rimfolk_Pool_SabotageConfront", rival, null, "form:" + form.ToLowerInvariant());
                    CouncilGameComponent.Instance?.social.Bump(other, rival, TieKind.Rivals, HarmTwoMath.SabotageRivalry, "CC_HarmTwo_TieNote_Sabotage".Translate());
                    bool fight = Harm.Fight(other, rival, HarmKinds.Sabotage);
                    string how = witness != null ? "CC_HarmTwo_SabotageFound_Seen".Translate(witness.Named("WITNESS")).Resolve() : "CC_HarmTwo_SabotageFound_Word".Translate().Resolve();
                    string text = TextVariants.Vary(fight ? "CC_HarmTwo_SabotageFight" : "CC_HarmTwo_SabotageWords")
                        .Translate(other.Named("OTHER"), rival.Named("PAWN"), what.Named("WHAT"), how.Named("HOW")).Resolve();
                    Tell.Message(NewsKind.Colony, fight ? Importance.Important : Importance.Notable, other.Map, text, new LookTargets(new[] { other, rival }), MessageTypeDefOf.NegativeEvent);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("harm: a sabotage had out", e);
                }
            }, 24f, 0f);
        }

        private static readonly Color SayColor = new Color(1f, 0.75f, 0.6f);

        private static void Say(Pawn p, string pool, Pawn to, Pawn about = null, string tag = null)
        {
            try
            {
                if (p == null) return;
                string line = Flavor.FromPool(pool, p, null, null, tag);
                if (line.NullOrEmpty()) return;
                var args = new List<NamedArgument> { p.Named("PAWN") };
                if (to != null) args.Add(to.Named("OTHER"));
                if (about != null) args.Add(about.Named("ABOUT"));
                // together: a line to the other, shown only while the two are together (Speech.Say checks it).
                Speech.Say(p, line.Formatted(args.ToArray()).Resolve(), to, SayColor);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("harm: a sabotage line from " + pool, e);
            }
        }
    }

    /// <summary>
    /// A rival at the other's bench (Sabotage): there, a few seconds at it with their back to the room, then what it does.
    /// One of theirs on a scheme against the colony uses it at a machine in the night (Diplomacy/Schemes.cs, which says
    /// what comes of it); a machine with no place to stand at is worked from beside it.
    /// </summary>
    public class JobDriver_Sabotage : JobDriver
    {
        private Building_WorkTable Bench => job.targetA.Thing as Building_WorkTable;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);

        public override string GetReport()
        {
            var t = job.targetA.Thing;
            return t != null ? "CC_HarmTwo_Report_Sabotage".Translate(t.LabelShort.Named("BENCH")).Resolve() : base.GetReport();
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOn(() => pawn.Map == null || pawn.Map.dangerWatcher.DangerRating != StoryDanger.None);
            var target = job.targetA.Thing;
            yield return Toils_Goto.GotoThing(TargetIndex.A, target != null && target.def.hasInteractionCell ? PathEndMode.InteractionCell : PathEndMode.Touch);
            var at = Toils_General.Wait(240, TargetIndex.A);
            at.WithProgressBarToilDelay(TargetIndex.A);
            yield return at;
            var done = ToilMaker.MakeToil("SabotageDone");
            // A scheme of theirs at a machine is the schemes' to resolve; a rival's at a bench, the sabotage's.
            done.initAction = () =>
            {
                if (!Schemes.Done(pawn, job.targetA.Thing)) Sabotage.Done(pawn, Bench);
            };
            done.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return done;
        }
    }
}
