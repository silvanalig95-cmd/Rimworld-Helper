using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Harm, part 4: hurts from the colony's own animals (Harm/HarmFour.cs is the hub). The animals are picked by what they
    /// are, never by a list, so a modded herd counts as the game's does.
    /// <list type="bullet">
    /// <item>The rut in the pens (4.5.3): in the autumn rut (the land's own seasons; the third quadrum where there are none),
    /// a grown male of a herd's grazers (a ram, a buck, a bull: never a predator, a body of 0.9 or more) penned with a female
    /// of his kind may charge one of the colony near him in his pen: about one charge in twenty such hours, half of them
    /// landing (a bruise, a crack one time in five), the rest jumping clear. Never the herdmaster, whose work it is; half
    /// for a handler of Animals 10 or more. Keeping out of the pens in the rut prevents it. Told as the game tells an animal
    /// turning on its handler. With the animals' life.</item>
    /// <item>The herdmaster's lesson (4.5.1): a learner following the herdmaster about the animals (the lesson, played out
    /// on the map) is kicked or bitten one lesson in 30 when the herdmaster handles a nervous animal (wild at heart, or
    /// timid by nature) with the learner beside it: a kick's bruise from a big one, a bite from a small one, a small bruise
    /// for a child. The herdmaster calms it. With the offices, answers on the map and "Systems talk to each other".</item>
    /// <item>A fall from the saddle (4.9.1): with Giddy-Up, a rider short of Animals 6 getting down from a mount not yet
    /// trained to obey is thrown one ride in 50: a bruise, and a few moments on the ground. Riders of Animals 6 or more, or
    /// a mount trained in obedience, never are. With the animals' life; nothing at all without Giddy-Up.</item>
    /// </list>
    /// </summary>
    public static class AnimalHurts
    {
        private static int Now => Find.TickManager.TicksGame;

        public static void Reset()
        {
            lessons.Clear();
            ridden.Clear();
        }

        // ================= The rut in the pens (4.5.3) =================

        /// <summary>It's the rut on this map: autumn by the land's seasons, or the third quadrum where there are none.</summary>
        public static bool RutNow(Map map)
        {
            if (map == null) return false;
            var season = GenLocalDate.Season(map);
            int quadrum = (int)GenDate.Quadrum(Find.TickManager.TicksAbs, Find.WorldGrid.LongLatOf(map.Tile).x);
            return HarmFourMath.RutSeason((int)season, quadrum);
        }

        /// <summary>A grown male of a herd's grazers in the rut, up and about, penned, with a female of his kind among the colony's animals.</summary>
        public static bool Rutting(Pawn a)
        {
            if (!AnimalFacts.Ours(a) || !a.Spawned || a.Downed || !a.Awake() || a.InMentalState) return false;
            var race = a.RaceProps;
            if (race == null || !HarmFourMath.Ruts(a.gender == Gender.Male, a.ageTracker?.CurLifeStage?.reproductive ?? false, race.predator, race.herdAnimal, a.BodySize)) return false;
            if (AnimalPenUtility.GetCurrentPenOf(a, false) == null) return false;
            var mates = a.Map.mapPawns.SpawnedColonyAnimals;
            for (int i = 0; i < mates.Count; i++)
                if (mates[i].def == a.def && mates[i].gender == Gender.Female) return true;
            return false;
        }

        /// <summary>Each colonist's hour: near a rutting male in his own pen, the chance of his charge.</summary>
        public static void RutHour(Pawn p, int now)
        {
            if (!Harm.On || !HarmFour.Ours(p) || !Together.Able(p) || !Harm.SystemOn(HarmKinds.Rut.system) || !RutNow(p.Map)) return;
            var region = p.GetRegion();
            if (region == null) return;
            Pawn bull = null;
            foreach (var a in p.Map.mapPawns.SpawnedColonyAnimals)
            {
                if (!a.Position.InHorDistOf(p.Position, HarmFourMath.RutRange) || !Rutting(a)) continue;
                var pen = AnimalPenUtility.GetCurrentPenOf(a, false);
                if (pen?.PenState == null || !pen.PenState.ContainsConnectedRegion(region)) continue;
                bull = a;
                break;
            }
            if (bull == null) return;
            float chance = HarmFourMath.RutCharge(AnimalFacts.IsHerdmaster(p), p.skills?.GetSkill(SkillDefOf.Animals)?.Level ?? 0);
            if (chance <= 0f || !Harm.Roll(p, HarmKinds.Rut, chance, HurtClass.Injury)) return;
            Charge(bull, p, Rand.Chance(HarmFourMath.RutLands));
        }

        /// <summary>The charge: he lowers his head and comes; it lands (a bruise, a crack now and then) or they jump clear.</summary>
        private static void Charge(Pawn bull, Pawn p, bool lands)
        {
            bull.rotationTracker?.FaceTarget(p);
            bull.Drawer?.Notify_MeleeAttackOn(p);
            FleckMaker.ThrowDustPuff(bull.Position, bull.Map, 1.2f);
            if (!lands)
            {
                HarmFour.Say(p, "Rimfolk_Pool_RutClear");
                return;
            }
            bool crack = Rand.Chance(HarmFourMath.RutCrackShare);
            var h = crack ? Harm.Injure(p, HurtKind.Crack, HurtWhere.Leg, HarmFourMath.RutCrackLow, HarmFourMath.RutCrackHigh, HarmKinds.Rut) : null;
            if (h == null)
            {
                crack = false;
                h = Harm.Injure(p, HurtKind.Bruise, Rand.Bool ? HurtWhere.Torso : HurtWhere.Leg, HarmFourMath.RutLow, HarmFourMath.RutHigh, HarmKinds.Rut);
            }
            if (h == null)
            {
                HarmFour.Say(p, "Rimfolk_Pool_RutClear");
                return;
            }
            HarmFour.SitDown(p, 300);
            HarmFour.Say(p, "Rimfolk_Pool_RutCharged", null, crack ? "crack" : "bruise");
            HarmFour.TellHurt(p, "CC_HarmFour_Rut", p.Named("PAWN"), bull.Named("ANIMAL"), (h.Part?.Label ?? "").Named("PART"));
        }

        /// <summary>Dev mode: the nearest rutting male charges this colonist now (in the rut or not), landing; never past the cap.</summary>
        public static string DebugRut(Pawn p)
        {
            var bull = p.Map.mapPawns.SpawnedColonyAnimals.Where(a => a.gender == Gender.Male && a.RaceProps.herdAnimal && !a.RaceProps.predator && !a.Downed)
                .OrderBy(a => a.Position.DistanceToSquared(p.Position)).FirstOrDefault();
            if (bull == null) return "no male of a herd's grazers among the colony's animals here.";
            if (!Harm.May(p)) return p.LabelShort + " is held back by the cap (" + Harm.Bar(p) + ").";
            Charge(bull, p, true);
            return bull.LabelShort + " charged " + p.LabelShort + (RutNow(p.Map) ? " (it is the rut)." : " (it isn't the rut here now: in play it would wait for autumn).");
        }

        // ================= The herdmaster's lesson (4.5.1) =================

        /// <summary>The lessons already rolled for (a play-out's id), session memory: one roll a lesson.</summary>
        private static readonly HashSet<int> lessons = new HashSet<int>();

        private static bool debugKick;

        /// <summary>The hour: each herdmaster's lesson under way, once the herdmaster handles a nervous animal with the learner beside it.</summary>
        public static void Lessons(int now)
        {
            var tracker = PlayOutTracker.Instance;
            if (tracker == null || !Harm.On || !Harm.SystemOn(HarmKinds.HerdLesson.system)) return;
            foreach (var p in tracker.plays)
            {
                if (p == null || p.kind != PlayKind.Lesson || p.state != PlayState.Running || p.skill != SkillDefOf.Animals || lessons.Contains(p.id)) continue;
                var hm = p.other;
                var learner = p.visitor;
                if (hm == null || learner == null || !hm.Spawned || !learner.Spawned || hm.Map != learner.Map || !AnimalFacts.IsHerdmaster(hm)) continue;
                var job = hm.CurJob;
                if (job == null || job.workGiverDef?.workType != WorkTypeDefOf.Handling || !(job.targetA.Thing is Pawn animal) || animal.RaceProps?.Animal != true) continue;
                if (!animal.Position.InHorDistOf(learner.Position, HarmFourMath.LessonRange) || !Together.Able(learner)) continue;
                bool timid = AnimalTempers.On && AnimalTempers.Of(animal) == Temper.Timid;
                if (!HarmFourMath.Nervous(animal.GetStatValue(StatDefOf.Wildness), timid) && !debugKick) continue;
                lessons.Add(p.id);
                if (lessons.Count > 200) lessons.Clear();
                var cls = HarmFour.Child(learner) ? HurtClass.Mild : HurtClass.Injury;
                bool kicks = debugKick ? Harm.May(learner, cls) : Harm.Roll(learner, HarmKinds.HerdLesson, HarmFourMath.LessonKickChance, cls);
                debugKick = false;
                if (!kicks) continue;
                Kick(animal, learner, hm, cls);
            }
        }

        private static void Kick(Pawn animal, Pawn learner, Pawn hm, HurtClass cls)
        {
            animal.rotationTracker?.FaceTarget(learner);
            animal.Drawer?.Notify_MeleeAttackOn(learner);
            bool big = animal.BodySize >= HarmFourMath.KickSize;
            Hediff_Injury h;
            if (cls == HurtClass.Mild) h = Harm.Injure(learner, HurtKind.Bruise, HurtWhere.Leg, HarmFourMath.ChildLow, HarmFourMath.ChildHigh, HarmKinds.HerdLesson, cls);
            else if (big) h = Harm.Injure(learner, HurtKind.Bruise, Rand.Bool ? HurtWhere.Leg : HurtWhere.Torso, HarmFourMath.KickLow, HarmFourMath.KickHigh, HarmKinds.HerdLesson, cls);
            else h = Harm.Injure(learner, HurtKind.Scratch, Rand.Bool ? HurtWhere.Hand : HurtWhere.Arm, HarmFourMath.BiteLow, HarmFourMath.BiteHigh, HarmKinds.HerdLesson, cls);
            if (h == null) return;
            HarmFour.SitDown(learner, 200);
            HarmFour.Say(learner, "Rimfolk_Pool_LessonKicked", null, big ? "kick" : "bite");
            // together: the herdmaster is at the animal, the learner beside it (LessonRange).
            HarmFour.Say(hm, "Rimfolk_Pool_HerdCalms", learner);
            HarmFour.TellHurt(learner, big ? "CC_HarmFour_LessonKick" : "CC_HarmFour_LessonBite", learner.Named("PAWN"), animal.Named("ANIMAL"), hm.Named("OTHER"));
        }

        /// <summary>Dev mode: the next lesson's handling turns into a kick, past its rate and its nervous animal, never past the cap.</summary>
        public static string DebugLesson()
        {
            debugKick = true;
            return "the next herdmaster's lesson kicks the learner once the herdmaster handles an animal with the learner beside it (Systems talk to each other, answers on the map).";
        }

        // ================= A fall from the saddle (4.9.1) =================

        /// <summary>A mount (Giddy-Up's mounted animal): its rider, while it carries them.</summary>
        private static readonly Dictionary<Pawn, Pawn> ridden = new Dictionary<Pawn, Pawn>();

        private static bool debugThrow;

        /// <summary>Giddy-Up's ridden animal does its job "Mounted", the rider its target.</summary>
        private static Pawn RiderOf(Pawn a)
        {
            var job = a.CurJob;
            if (job?.def?.defName == null || !job.def.defName.EndsWith("Mounted", StringComparison.Ordinal)) return null;
            return job.targetA.Thing as Pawn;
        }

        /// <summary>Every 250 ticks with Giddy-Up: the colony's mounts and riders; a ride just over is a dismount, and its one roll.</summary>
        public static void Riders(int now)
        {
            if (!Harm.On || !AnimalFacts.GiddyUp || !Harm.SystemOn(HarmKinds.Saddle.system)) return;
            var maps = Find.Maps;
            for (int m = 0; m < maps.Count; m++)
            {
                var map = maps[m];
                if (!map.IsPlayerHome) continue;
                var animals = map.mapPawns.SpawnedColonyAnimals;
                for (int i = 0; i < animals.Count; i++)
                {
                    var rider = RiderOf(animals[i]);
                    if (rider != null) ridden[animals[i]] = rider;
                }
            }
            if (ridden.Count == 0) return;
            foreach (var kv in ridden.ToList())
            {
                var mount = kv.Key;
                if (mount == null || mount.Dead || !mount.Spawned)
                {
                    ridden.Remove(mount);
                    continue;
                }
                if (RiderOf(mount) == kv.Value) continue;
                ridden.Remove(mount);
                Dismounted(mount, kv.Value);
            }
        }

        private static void Dismounted(Pawn mount, Pawn rider)
        {
            if (!HarmFour.Ours(rider) || !rider.Spawned || rider.Map != mount.Map || !rider.Position.InHorDistOf(mount.Position, 3f) || rider.Downed) return;
            bool obedient = mount.training?.HasLearned(TrainableDefOf.Obedience) ?? false;
            float chance = HarmFourMath.SaddleThrow(rider.skills?.GetSkill(SkillDefOf.Animals)?.Level ?? 0, obedient);
            var cls = HarmFour.Child(rider) ? HurtClass.Mild : HurtClass.Injury;
            bool thrown = debugThrow ? Harm.May(rider, cls) : chance > 0f && Harm.Roll(rider, HarmKinds.Saddle, chance, cls);
            debugThrow = false;
            if (!thrown) return;
            // The mount bucks at the getting down, and the rider comes off hard.
            FleckMaker.ThrowDustPuff(mount.Position, mount.Map, 1.5f);
            mount.Drawer?.Notify_MeleeAttackOn(rider);
            var h = cls == HurtClass.Mild
                ? Harm.Injure(rider, HurtKind.Bruise, HurtWhere.Leg, HarmFourMath.SaddleChildLow, HarmFourMath.SaddleChildHigh, HarmKinds.Saddle, cls)
                : Harm.Injure(rider, HurtKind.Bruise, Rand.Bool ? HurtWhere.Torso : HurtWhere.Leg, HarmFourMath.SaddleLow, HarmFourMath.SaddleHigh, HarmKinds.Saddle, cls);
            if (h == null) return;
            HarmFour.SitDown(rider, 300);
            HarmFour.Say(rider, "Rimfolk_Pool_Thrown");
            HarmFour.Mote(rider, "CC_HarmFour_Mote_Thrown", mount.Named("ANIMAL"));
        }

        /// <summary>Dev mode: the next dismount throws its rider, past its rate and the rider's skill, never past the cap.</summary>
        public static string DebugSaddle()
        {
            debugThrow = true;
            return AnimalFacts.GiddyUp ? "the next rider to get down from a mount here is thrown." : "needs Giddy-Up.";
        }
    }
}
