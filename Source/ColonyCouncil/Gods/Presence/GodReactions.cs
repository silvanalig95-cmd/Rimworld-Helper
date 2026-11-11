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
    /// <summary>
    /// The world turning toward a god (docs/design/gods.md, "Presence"): at its coming everyone on the map stops and
    /// turns to it for a few seconds (a short job), the devout pray with a small light over them, calm animals lie down
    /// facing it and shy wild ones run; as a shown god walks by, those near who see it stop and look, and one of them
    /// says so in a bubble now and then (one line in two hours at most, the same person once in two days). A god in
    /// disguise shows only its tells: animals take to following it, a shimmer about it at night, and a colonist with a
    /// sharp eye or a psychic gift remarks on it (once a day), with no footprints in the snow behind it
    /// (GodPresencePatches.cs). Who answers and how is GodPresenceMath.ReactionTo; nothing here runs but for a god, a few
    /// times a minute.
    /// </summary>
    public static class GodReactions
    {
        public const string BeholdJob = "Rimfolk_BeholdGod";
        public const string PrayJob = "Rimfolk_PrayToGod";
        public const string LieJob = "Rimfolk_LieBeforeGod";

        public const string AwePool = "Rimfolk_Pool_GodAwe";
        public const string MomentPool = "Rimfolk_Pool_GodMoment";
        public const string PrayerPool = "Rimfolk_Pool_GodPrayer";
        public const string TellPool = "Rimfolk_Pool_GodTell";
        public const string OmenPool = "Rimfolk_Pool_GodOmen";

        // When each person last answered a god and last said something about one (not saved: a few hours' memory).
        private static readonly Dictionary<int, int> answered = new Dictionary<int, int>();
        private static readonly Dictionary<int, int> spoke = new Dictionary<int, int>();
        private static Game game;

        private static void Fresh()
        {
            if (Current.Game == game) return;
            game = Current.Game;
            answered.Clear();
            spoke.Clear();
        }

        private static JobDef Job(string name) => DefDatabase<JobDef>.GetNamedSilentFail(name);

        // ================= Reading someone =================

        private static GodWatcher Watcher(Pawn w, IntVec3 at, Map map, bool needSight)
        {
            var job = w.CurJob;
            var lord = w.GetLord();
            bool animal = w.RaceProps.Animal;
            return new GodWatcher
            {
                animal = animal,
                colonist = w.IsColonist,
                hostile = w.HostileTo(Faction.OfPlayer),
                drafted = w.Drafted,
                downed = w.Downed,
                asleep = !w.Awake(),
                mentalState = w.InMentalState,
                // Busy: a job that mustn't be broken off, something carried, a ritual or caravan of the colony's own, a roped animal.
                busy = (job != null && !job.def.casualInterruptible) || w.carryTracker?.CarriedThing != null || (lord != null && w.IsColonist)
                       || (animal && (w.roping?.IsRoped ?? false)) || IsGodJob(job) || w.IsPrisoner,
                devout = !animal && Devout(w),
                tame = animal && w.Faction == Faction.OfPlayer,
                predatorHunting = animal && job != null && job.def == JobDefOf.PredatorHunt,
                wildness = animal ? w.def.GetStatValueAbstract(StatDefOf.Wildness) : 0f,
                distance = w.Position.DistanceTo(at),
                sees = !needSight || GenSight.LineOfSight(w.Position, at, map, true)
            };
        }

        private static bool IsGodJob(Job j) => j != null && (j.def.defName == BeholdJob || j.def.defName == PrayJob || j.def.defName == LieJob);

        /// <summary>The devout: strong in their faith (Ideology's certainty), or a priest, paladin, druid or monk of A RimWorld of Magic.</summary>
        private static bool Devout(Pawn p)
        {
            if (ModsConfig.IdeologyActive && p.ideo != null && p.Ideo != null && p.ideo.Certainty >= 0.8f) return true;
            var traits = p.story?.traits;
            if (traits == null) return false;
            foreach (var t in traits.allTraits)
                if (t?.def != null && (t.def.defName == "Priest" || t.def.defName == "Paladin" || t.def.defName == "Druid" || t.def.defName == "TM_Monk"))
                    return true;
            return false;
        }

        // ================= Answering =================

        private static void Start(Pawn w, Pawn god, IntVec3 at, GodReaction r, bool moment)
        {
            try
            {
                if (r == GodReaction.Flee)
                {
                    if (god == null) return;
                    var flee = FleeUtility.FleeJob(w, god, 14);
                    if (flee != null) w.jobs.StartJob(flee, JobCondition.InterruptForced);
                    return;
                }
                var def = Job(r == GodReaction.Pray ? PrayJob : r == GodReaction.LieDown ? LieJob : BeholdJob);
                if (def == null) return;
                var job = JobMaker.MakeJob(def, god != null ? new LocalTargetInfo(god) : new LocalTargetInfo(at));
                job.count = GodPresenceMath.ReactTicks(r, moment) + Rand.Range(-40, 60);
                job.expiryInterval = job.count + 120;
                bool resume = w.CurJob != null && w.CurJob.def.suspendable;
                w.jobs.StartJob(job, JobCondition.InterruptForced, null, resume);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: someone turning to a god", e);
            }
        }

        private static bool Recently(Dictionary<int, int> book, Pawn p, int now, int wait) => book.TryGetValue(p.thingIDNumber, out int t) && now - t < wait;

        /// <summary>
        /// A line about a god from a pool, said aloud (one sentence): the lines that name it (tagged named) only for a god
        /// that is named, with its name in them.
        /// </summary>
        private static bool Say(Pawn p, string pool, string godName, params string[] tags)
        {
            if (!p.RaceProps.Humanlike) return false;
            var all = new List<string>(tags.Where(t => !t.NullOrEmpty()));
            if (!godName.NullOrEmpty()) all.Add("named");
            string line = Flavor.FromPool(pool, p, null, null, all.ToArray());
            if (line.NullOrEmpty()) return false;
            line = line.Replace("{NAME}", godName.NullOrEmpty() ? "CC_GodP_TheLight".Translate().Resolve() : godName);
            return Speech.Say(p, line);
        }

        // ================= A god passing =================

        /// <summary>Those near a shown god who see it stop and look; one in a while says so.</summary>
        internal static void Passing(HediffComp_GodAvatar c, Pawn god, int now)
        {
            Fresh();
            var map = god.Map;
            var pawns = map.mapPawns.AllPawnsSpawned;
            int stops = 0;
            for (int i = 0; i < pawns.Count && stops < GodPresenceMath.StopsAPass; i++)
            {
                var w = pawns[i];
                if (w == god || w.Dead || GodAvatar.Is(w) || !w.Position.InHorDistOf(god.Position, GodPresenceMath.NearCells)) continue;
                if (Recently(answered, w, now, GodPresenceMath.PersonCooldown)) continue;
                var r = GodPresenceMath.ReactionTo(Watcher(w, god.Position, map, true), false);
                if (r == GodReaction.None) continue;
                Start(w, god, god.Position, r, false);
                answered[w.thingIDNumber] = now;
                stops++;
                int last = spoke.TryGetValue(w.thingIDNumber, out int l) ? l : -1;
                if (GodPresenceMath.Speaks(now, c.lastSpoken, last, false) && Say(w, r == GodReaction.Pray ? PrayerPool : AwePool, c.Name, c.look.Domain.ToLowerInvariant(), Night(map, w) ? "night" : null))
                {
                    c.lastSpoken = now;
                    spoke[w.thingIDNumber] = now;
                }
            }
        }

        private static bool Night(Map map, Pawn p) => map.skyManager.CurSkyGlow < 0.35f;

        // ================= A god's coming =================

        /// <summary>
        /// Everyone on the map stops and turns toward the place of a god's coming (animals within forty cells; the shy
        /// ones near it run); the closest few people say something. An omen: only those within sixteen cells look up.
        /// </summary>
        internal static void Moment(Map map, IntVec3 at, Pawn god, string godName, string domain, PresenceKind kind)
        {
            Fresh();
            if (map == null) return;
            int now = Find.TickManager.TicksGame;
            bool omen = kind == PresenceKind.Omen;
            var turned = new List<Pawn>();
            foreach (var w in map.mapPawns.AllPawnsSpawned.ToList())
            {
                if (w == god || w.Dead || GodAvatar.Is(w)) continue;
                float d = w.Position.DistanceTo(at);
                if (omen ? d > GodPresenceMath.OmenCells : w.RaceProps.Animal && d > 40f) continue;
                var r = GodPresenceMath.ReactionTo(Watcher(w, at, map, omen), !omen);
                if (r == GodReaction.None) continue;
                if (omen && r != GodReaction.Flee) r = GodReaction.Face;
                Start(w, god, at, r, !omen);
                answered[w.thingIDNumber] = now;
                if (w.RaceProps.Humanlike) turned.Add(w);
            }
            int speakers = omen ? 1 : GodPresenceMath.MomentSpeakers;
            foreach (var w in turned.OrderBy(w => w.Position.DistanceToSquared(at)).Take(speakers))
            {
                string pool = omen ? OmenPool : Devout(w) && kind != PresenceKind.Wrath ? PrayerPool : MomentPool;
                if (Say(w, pool, godName, kind.ToString().ToLowerInvariant(), (domain ?? "").ToLowerInvariant(), Night(map, w) ? "night" : null)) spoke[w.thingIDNumber] = now;
            }
        }

        // ================= A god in disguise =================

        /// <summary>A disguised god's tells, looked at every few seconds: the shimmer at night, the animals, the remark.</summary>
        internal static void Tells(HediffComp_GodAvatar c, Pawn god, int now)
        {
            Fresh();
            var map = god.Map;
            bool dark = map.glowGrid.GroundGlowAt(god.Position) < GodPresenceMath.ShimmerDark;
            if (dark && GodAura.On && map == Find.CurrentMap && Rand.Chance(0.6f)) GodFx.Shimmer(god, c.ColorA);
            if (now - c.lastFollow >= GodPresenceMath.FollowEvery && Rand.Chance(0.35f) && Follow(god, map)) c.lastFollow = now;
            if (now - c.lastTell < GodPresenceMath.TellCooldown) return;
            bool snow = map.snowGrid.GetDepth(god.Position) >= 0.4f;
            bool animals = now - c.lastFollow < GodPresenceMath.FollowEvery * 2;
            foreach (var w in map.mapPawns.FreeColonistsSpawned)
            {
                float d = w.Position.DistanceTo(god.Position);
                if (d > 10f || !w.Awake() || w.InMentalState || w.Downed) continue;
                float sight = w.health.capacities.GetLevel(PawnCapacityDefOf.Sight);
                float psychic = w.GetStatValue(StatDefOf.PsychicSensitivity);
                if (!GodPresenceMath.NoticesTell(sight, psychic, dark, d) || !GenSight.LineOfSight(w.Position, god.Position, map, true)) continue;
                var tags = new List<string>();
                if (dark) tags.Add("night");
                if (snow) tags.Add("snow");
                if (animals) tags.Add("animals");
                if (psychic >= 1.5f) tags.Add("psychic");
                if (!god.RaceProps.Humanlike) tags.Add("animal");
                if (Say(w, TellPool, null, tags.ToArray()))
                {
                    c.lastTell = now;
                    return;
                }
            }
        }

        /// <summary>An animal near a disguised god takes to following it for a while.</summary>
        private static bool Follow(Pawn god, Map map)
        {
            var job = JobDefOf.FollowClose;
            foreach (var a in map.mapPawns.AllPawnsSpawned.ToList())
            {
                if (!a.RaceProps.Animal || a.Dead || GodAvatar.Is(a) || !a.Position.InHorDistOf(god.Position, 14f)) continue;
                var w = Watcher(a, god.Position, map, false);
                if (w.hostile || w.downed || w.asleep || w.mentalState || w.busy || w.predatorHunting || w.drafted) continue;
                var j = JobMaker.MakeJob(job, god);
                j.followRadius = 3f;
                j.expiryInterval = 1500;
                j.checkOverrideOnExpire = true;
                a.jobs.StartJob(j, JobCondition.InterruptForced);
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Stopping before a god: standing still facing it (or where it came), praying with a small light rising, or (an
    /// animal) lying down facing it, for a few seconds; then back to what they were doing. Ended by their being drafted,
    /// hurt or frightened, like any idle moment.
    /// </summary>
    public class JobDriver_BeholdGod : JobDriver
    {
        private bool Lies => job.def.defName == GodReactions.LieJob;

        private bool Prays => job.def.defName == GodReactions.PrayJob;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        public override string GetReport()
        {
            var god = job.targetA.Thing as Pawn;
            var c = GodAvatar.Of(god);
            string name = c != null && !c.disguised ? c.Name : null;
            string key = Prays ? "CC_GodP_Job_Pray" : Lies ? "CC_GodP_Job_Lie" : "CC_GodP_Job_Behold";
            if (name == null) return (key + "Place").Translate().Resolve();
            return key.Translate(name.Named("NAME")).Resolve();
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => pawn.Downed || pawn.InMentalState || pawn.Drafted);
            var stay = ToilMaker.MakeToil("BeholdGod");
            stay.initAction = () =>
            {
                pawn.pather?.StopDead();
                if (Lies) pawn.jobs.posture = PawnPosture.LayingOnGroundNormal;
            };
            stay.tickIntervalAction = Look;
            stay.defaultCompleteMode = ToilCompleteMode.Delay;
            stay.defaultDuration = Math.Max(60, job.count);
            stay.handlingFacing = true;
            stay.AddFinishAction(() =>
            {
                if (Lies) pawn.jobs.posture = PawnPosture.Standing;
            });
            yield return stay;
        }

        private void Look(int delta)
        {
            var t = job.targetA;
            if (t.HasThing)
            {
                if (!t.Thing.Spawned || t.Thing.Map != pawn.Map)
                {
                    EndJobWith(JobCondition.Succeeded);
                    return;
                }
                pawn.rotationTracker.FaceTarget(t.Thing);
            }
            else if (t.IsValid) pawn.rotationTracker.FaceCell(t.Cell);
            if (Prays && pawn.IsHashIntervalTick(100, delta) && pawn.Map == Find.CurrentMap)
            {
                var c = GodAvatar.Of(t.Thing as Pawn);
                GodFx.Prayer(pawn, c?.ColorA ?? new Color(1f, 0.9f, 0.6f));
            }
        }
    }
}
