using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>The small habits, each a way of spending free time that what Rimfolk knows of someone makes likelier.</summary>
    public enum HabitKind
    {
        /// <summary>Grieving: a visit to the grave of the one they lost.</summary>
        Grave,
        /// <summary>Homesick: a while alone just outside the colony, looking out.</summary>
        LookOut,
        /// <summary>After a hard fight: a while round the colony's fire with the others who fought.</summary>
        Fire,
        /// <summary>A friend left: a talk with someone about them.</summary>
        Remember,
        /// <summary>Convalescing: a slow walk (the game's own walk).</summary>
        Walk,
        /// <summary>A child after the first snow: out building a snowman (the game's own).</summary>
        Snow,
        /// <summary>Awed, or one who watches the stars: a while out under the night sky (Immersion/MoreHabits.cs).</summary>
        Stars,
        /// <summary>One who talks to the plants: a while by the crops or a plant pot, talking to them.</summary>
        Plants,
        /// <summary>Tender, or one who names every animal: a while with one of the colony's animals.</summary>
        Animals,
        /// <summary>Worried, or one who counts everything: a while going over the food stores.</summary>
        Stores,
        /// <summary>Lonely: they seek out someone at their leisure (a Hospitality guest too) and keep them company.</summary>
        Company,
        /// <summary>Nostalgic, of an evening: a while by the colony's fire, telling old stories.</summary>
        Stories
    }

    /// <summary>On a small habit's JobDef: which habit it is, what's said on arriving, and whether they chat with whoever is near.</summary>
    public class HabitExtension : DefModExtension
    {
        public HabitKind kind;
        /// <summary>The flavour pool of the line said on arriving ({OTHER} is whom it's about: the dead, the friend who left).</summary>
        public string pool;
        public bool social;
    }

    /// <summary>
    /// Small habits ("Small habits" setting): recreation the game's own joy system picks, made likelier by what Rimfolk
    /// knows of someone. A grieving colonist visits the grave; a homesick one sits a while alone just outside the colony,
    /// looking out; a convalescent takes slow walks; after a hard fight, those who fought sit round the fire if one is lit;
    /// someone whose friend left talks about them with whoever is free; and after the first snow, children go out to build
    /// snowmen. Six more come from the heart's feelings and quirks (<see cref="MoreHabits"/>): stars, plants, animals,
    /// the stores, company and old stories by the fire.
    /// </summary>
    /// <remarks>
    /// Each is a joy giver (Defs/Misc/Rimfolk_SmallHabits.xml), so it only ever comes up when the game itself looks for
    /// recreation: in free time, after danger, sleep, food and medical care have had their turn, and never while drafted,
    /// in a sickbed, starving, bleeding, exhausted, caring for someone, at a gathering or under the player's orders. Its
    /// job ends at any danger, when the setting is switched off, when their joy is full, and like any job when the player
    /// gives an order. Each habit comes round for the same colonist at most once in its cooldown (half a day to a day).
    /// What it checks is already known (memories, the heart's feelings, the social web, the last big threat); nothing is
    /// computed per tick, and nothing is saved.
    /// </remarks>
    public static class SmallHabits
    {
        public static bool On => ColonyCouncilMod.Settings != null && ColonyCouncilMod.Settings.smallHabits && Current.Game != null;

        private static JobDef slowWalk;
        private static ThoughtDef homesickThought;
        private static bool looked;

        private static void Look()
        {
            if (looked) return;
            looked = true;
            slowWalk = DefDatabase<JobDef>.GetNamedSilentFail("Council_Habit_SlowWalk");
            homesickThought = DefDatabase<ThoughtDef>.GetNamedSilentFail("Council_Setback_Homesick");
        }

        /// <summary>The convalescent's slow walk (the game's walk, under a name of its own).</summary>
        public static JobDef SlowWalkDef
        {
            get
            {
                Look();
                return slowWalk;
            }
        }

        // ---------------- Cooldowns ----------------

        private static readonly Dictionary<(int, HabitKind), int> until = new Dictionary<(int, HabitKind), int>();
        private static Game game;

        private static int Cooldown(HabitKind kind)
        {
            switch (kind)
            {
                case HabitKind.Walk: return GenDate.TicksPerDay * 2 / 5;
                case HabitKind.Snow:
                case HabitKind.Animals:
                case HabitKind.Company: return GenDate.TicksPerDay / 2;
                case HabitKind.Fire:
                case HabitKind.Plants:
                case HabitKind.Stories: return GenDate.TicksPerDay * 3 / 5;
                default: return GenDate.TicksPerDay;
            }
        }

        public static bool Ready(Pawn p, HabitKind kind, int now)
        {
            if (game != Current.Game)
            {
                game = Current.Game;
                until.Clear();
            }
            return !(until.TryGetValue((p.thingIDNumber, kind), out int t) && t > now);
        }

        public static void Done(Pawn p, HabitKind kind, int now)
        {
            if (p == null) return;
            if (until.Count > 400)
            {
                var gone = new List<(int, HabitKind)>();
                foreach (var kv in until)
                    if (kv.Value <= now) gone.Add(kv.Key);
                foreach (var k in gone) until.Remove(k);
            }
            until[(p.thingIDNumber, kind)] = now + Cooldown(kind);
        }

        // ---------------- Who may ----------------

        /// <summary>
        /// One of the colony's own, up and about and free: not drafted, in a mental state, a sickbed or in need of one,
        /// bleeding, starving or exhausted, not caring for anyone or carrying them, not under orders, at a gathering or in
        /// the sleep hours, and nothing dangerous on the map.
        /// </summary>
        public static bool Free(Pawn p)
        {
            if (p == null || !p.Spawned || p.Map == null || p.Dead || p.Downed || p.Drafted || p.InMentalState || !p.Awake()) return false;
            if (!p.IsFreeColonist || p.IsQuestLodger() || p.DevelopmentalStage.Baby() || !People.HasInnerLife(p)) return false;
            if (p.Map.dangerWatcher.DangerRating != StoryDanger.None || p.GetLord() != null) return false;
            if (p.timetable != null && p.timetable.CurrentAssignment == TimeAssignmentDefOf.Sleep) return false;
            return PlayOuts.CanJoin(p);
        }

        /// <summary>The setting, the colonist free, the habit off cooldown and theirs to have: the one check every habit's joy giver makes.</summary>
        public static bool Allowed(Pawn p, HabitKind kind, Func<Pawn, bool> fits)
        {
            try
            {
                return On && Free(p) && Ready(p, kind, Find.TickManager.TicksGame) && fits(p);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("small habit " + kind, e);
                return false;
            }
        }

        // ---------------- Theirs to have ----------------

        /// <summary>Someone they're grieving (a death in the last twenty days) whose grave is on this map, and the grave.</summary>
        public static Pawn GriefFor(Pawn p, out Thing grave)
        {
            grave = null;
            var mem = p?.needs?.mood?.thoughts?.memories?.Memories;
            if (mem == null || p.Map == null) return null;
            Pawn best = null;
            int bestAge = int.MaxValue;
            foreach (var m in mem)
            {
                if (m?.def == null || m.otherPawn == null || !m.otherPawn.Dead || m.age > GenDate.TicksPerDay * 20 || m.age >= bestAge) continue;
                if (Array.IndexOf(Flavor.GriefThoughts, m.def.defName) < 0) continue;
                var g = PlaySpots.GraveOf(p.Map, m.otherPawn.ThingID);
                if (g == null) continue;
                best = m.otherPawn;
                bestAge = m.age;
                grave = g;
            }
            return best;
        }

        /// <summary>Homesick: the heart's feeling running at least a third strong, or the setback of thinking of home.</summary>
        public static bool Homesick(Pawn p)
        {
            var h = Heart.Of(p);
            if (h?.feelings != null)
                foreach (var f in h.feelings)
                    if (f?.def?.key == "Homesick" && HeartMath.Share(f.intensity) >= 0.35f) return true;
            Look();
            return homesickThought != null && p.needs?.mood?.thoughts?.memories?.GetFirstMemoryOfDef(homesickThought) != null;
        }

        /// <summary>Getting back on their feet, and well enough to walk.</summary>
        public static bool Convalescing(Pawn p) =>
            HealthTracker.ConvalescenceOn && HealthTracker.ConvalescenceOf(p) != null && p.health?.capacities != null
            && p.health.capacities.GetLevel(PawnCapacityDefOf.Moving) >= 0.4f;

        /// <summary>
        /// The colony's lit fire, for someone who fought in the last big threat on this map: within about half a day of it
        /// beginning, once it's over.
        /// </summary>
        public static Thing FireAfterFight(Pawn p)
        {
            var map = p.Map;
            int big = map?.storyState?.LastThreatBigTick ?? 0;
            int now = Find.TickManager.TicksGame;
            if (big <= 0 || now - big > GenDate.TicksPerDay * 3 / 5 || now - big < GenDate.TicksPerHour) return null;
            if (p.mindState == null || p.mindState.lastAttackTargetTick < big) return null;
            var fire = PlaySpots.ColonyFire(map, p);
            if (fire == null || fire.IsForbidden(p) || !fire.Position.InHorDistOf(p.Position, 80f)) return null;
            return fire;
        }

        /// <summary>A friend (a bond in the social web) who left the colony alive: walked out, was banished, was taken.</summary>
        public static Pawn FriendWhoLeft(Pawn p)
        {
            var web = CouncilGameComponent.Instance?.social;
            if (web?.ties == null || !(ColonyCouncilMod.Settings?.socialWeb ?? false)) return null;
            foreach (var t in web.ties)
            {
                if (t == null || t.kind != TieKind.Friends || t.strength < 15f || !t.Involves(p)) continue;
                var o = t.Other(p);
                if (Left(o, p)) return o;
            }
            return null;
        }

        private static bool Left(Pawn o, Pawn p) =>
            o != null && !o.Dead && !o.Destroyed && o.RaceProps != null && o.RaceProps.Humanlike && !o.IsColonist && o.Faction != Faction.OfPlayer
            && !o.IsPrisonerOfColony && !o.IsSlaveOfColony && !(o.Spawned && o.Map == p.Map);

        /// <summary>
        /// A cell by the grave or the fire for this colonist to stand on: free, and nobody else standing there or on their
        /// way to it, so those who come together stand side by side rather than on one spot. Invalid when there's none.
        /// </summary>
        public static IntVec3 SpotNear(IntVec3 c, Map map, Pawn p) => SpotNear(c, map, p, null);

        /// <summary>The same, among cells that also pass <paramref name="extra"/> (out under the open sky, say).</summary>
        public static IntVec3 SpotNear(IntVec3 c, Map map, Pawn p, Predicate<IntVec3> extra)
        {
            var dest = map.pawnDestinationReservationManager;
            foreach (var x in GenRadial.RadialCellsAround(c, 3.9f, true))
            {
                if (!x.InBounds(map) || x.GetEdifice(map) != null || !dest.CanReserve(x, p)) continue;
                var there = x.GetFirstPawn(map);
                if (there != null && there != p) continue;
                if (extra != null && !extra(x)) continue;
                if (PlaySpots.Ok(x, map, p)) return x;
            }
            return IntVec3.Invalid;
        }

        /// <summary>Someone nearby with time to listen: at their leisure or idle, awake and able to talk; one who knew the friend first.</summary>
        public static Pawn ListenerFor(Pawn p, Pawn friend)
        {
            Pawn best = null;
            float bestScore = float.MinValue;
            foreach (var o in p.Map.mapPawns.FreeColonistsSpawned)
            {
                if (o == p || !CanListen(o) || !o.Position.InHorDistOf(p.Position, 40f)) continue;
                float score = Rand.Value;
                if (friend != null && o.relations != null && o.relations.OpinionOf(friend) > 10) score += 1f;
                if (score <= bestScore || !p.CanReach(o, PathEndMode.Touch, Danger.None)) continue;
                best = o;
                bestScore = score;
            }
            return best;
        }

        /// <summary>Awake, able to talk, and at their leisure (recreation or wandering about), not working or under orders.</summary>
        public static bool CanListen(Pawn o)
        {
            if (o == null || !o.Spawned || o.Dead || o.Downed || o.Drafted || o.InMentalState || !o.Awake() || o.GetLord() != null) return false;
            if (!People.HasInnerLife(o) || o.DevelopmentalStage.Baby() || o.health?.capacities == null || !o.health.capacities.CapableOf(PawnCapacityDefOf.Talking)) return false;
            var job = o.CurJob;
            if (job == null || job.playerForced) return false;
            return job.def.joyKind != null || job.def == JobDefOf.Wait_Wander || job.def == JobDefOf.GotoWander;
        }

        // ---------------- What's said on arriving ----------------

        private static readonly Color SpeechColor = new Color(0.9f, 0.92f, 0.88f);

        /// <summary>A line on arriving, in view, from someone who hasn't said anything lately (it shares the touches' once-a-day rate).</summary>
        public static void Say(Pawn p, HabitExtension ext, Pawn about)
        {
            if (ext == null || ext.pool.NullOrEmpty() || !VisualTouches.On || !Speech.On) return;
            var vt = VisualTouches.Instance;
            if (vt == null || !VisualTouches.InView(out var map, out var view) || map != p.Map) return;
            int now = Find.TickManager.TicksGame;
            if (!vt.Free(p, map, view, now)) return;
            var tags = new List<string>();
            if (about != null)
            {
                if (about.RaceProps?.Animal == true)
                {
                    tags.Add("animal");
                    if (about.relations != null && about.relations.DirectRelationExists(PawnRelationDefOf.Bond, p)) tags.Add("bonded");
                    if (about.Name != null && !about.Name.Numerical) tags.Add("named");
                }
                else if (Strangers.IsHospitalityGuest(about)) tags.Add("guest");
                else if (ArcUtil.Partners(p, about)) tags.Add("partner");
                else if (ArcUtil.Kin(p, about)) tags.Add("kin");
                if (about.DevelopmentalStage.Juvenile()) tags.Add("young");
            }
            string line = Flavor.FromPool(ext.pool, p, null, null, tags.ToArray());
            if (line.NullOrEmpty()) return;
            line = line.Formatted(p.Named("PAWN"), (about ?? p).Named("OTHER")).Resolve();
            // together: keeping someone company, the line is said to them through Speech.Say, which shows it only when
            // the two are really together; the other habits' lines are about someone absent (the dead, a friend gone) or
            // said to an animal or to nobody.
            bool said = ext.kind == HabitKind.Company && about != null ? Speech.Say(p, line, about, SpeechColor) : Speech.Say(p, line, null, SpeechColor);
            if (said) vt.Spoke(p, now + GenDate.TicksPerDay / 2);
        }
    }

    // ---------------- The joy givers ----------------

    /// <summary>A small habit's joy giver: no chance at all unless the setting is on, they're free, it's off cooldown and it's theirs.</summary>
    public abstract class JoyGiver_Habit : JoyGiver
    {
        protected abstract HabitKind Kind { get; }

        protected abstract bool Fits(Pawn p);

        protected abstract Job Give(Pawn p);

        public override float GetChance(Pawn pawn) => SmallHabits.Allowed(pawn, Kind, Fits) ? base.GetChance(pawn) : 0f;

        public override bool CanBeGivenTo(Pawn pawn) => SmallHabits.Allowed(pawn, Kind, Fits) && base.CanBeGivenTo(pawn);

        public override Job TryGiveJob(Pawn pawn)
        {
            if (!SmallHabits.Allowed(pawn, Kind, Fits)) return null;
            try
            {
                return Give(pawn);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("small habit job " + Kind, e);
                return null;
            }
        }

        /// <summary>Never at a party: these are things done alone or with a few.</summary>
        public override Job TryGiveJobInGatheringArea(Pawn pawn, IntVec3 gatherSpot, float maxRadius = -1f) => null;

        public override Job TryGiveJobWhileInBed(Pawn pawn) => null;

        protected Job Make(LocalTargetInfo a, LocalTargetInfo b, IntRange ticks)
        {
            var job = JobMaker.MakeJob(def.jobDef, a, b);
            job.count = ticks.RandomInRange;
            return job;
        }
    }

    /// <summary>Grieving: a while standing at the grave of the one they lost (if it's on this map and the weather lets them out).</summary>
    public class JoyGiver_HabitGrave : JoyGiver_Habit
    {
        protected override HabitKind Kind => HabitKind.Grave;

        protected override bool Fits(Pawn p) => SmallHabits.GriefFor(p, out _) != null;

        protected override Job Give(Pawn p)
        {
            if (SmallHabits.GriefFor(p, out var grave) == null || grave == null) return null;
            if (!grave.Position.Roofed(p.Map) && !JoyUtility.EnjoyableOutsideNow(p)) return null;
            var cell = SmallHabits.SpotNear(grave.Position, p.Map, p);
            return cell.IsValid ? Make(cell, grave, new IntRange(1500, 2400)) : null;
        }
    }

    /// <summary>Homesick: a while alone just outside the colony, facing away from it, in weather fit for being out.</summary>
    public class JoyGiver_HabitLookOut : JoyGiver_Habit
    {
        protected override HabitKind Kind => HabitKind.LookOut;

        protected override bool Fits(Pawn p) => SmallHabits.Homesick(p);

        protected override Job Give(Pawn p)
        {
            var map = p.Map;
            if (!JoyUtility.EnjoyableOutsideNow(p) || !RCellFinder.TryFindRandomSpotJustOutsideColony(p, out var c)) return null;
            if (!c.InHorDistOf(p.Position, 70f) || c.Roofed(map) || !PlaySpots.Ok(c, map, p)) return null;
            var center = TemplatePlanner.ColonyCenter(map);
            var away = (c - center).ToVector3();
            if (away.sqrMagnitude < 1f) away = new Vector3(0f, 0f, 1f);
            var face = (c.ToVector3Shifted() + away.normalized * 6f).ToIntVec3().ClampInsideMap(map);
            return Make(c, face, new IntRange(1500, 2400));
        }
    }

    /// <summary>After a hard fight: a while by the colony's lit fire, chatting with whoever else came.</summary>
    public class JoyGiver_HabitFire : JoyGiver_Habit
    {
        protected override HabitKind Kind => HabitKind.Fire;

        protected override bool Fits(Pawn p) => SmallHabits.FireAfterFight(p) != null;

        protected override Job Give(Pawn p)
        {
            var fire = SmallHabits.FireAfterFight(p);
            if (fire == null || !p.CanReach(fire, PathEndMode.Touch, Danger.None)) return null;
            var cell = SmallHabits.SpotNear(fire.Position, p.Map, p);
            return cell.IsValid ? Make(cell, fire, new IntRange(1000, 1800)) : null;
        }
    }

    /// <summary>A friend left: they find someone at their leisure and talk about them a while.</summary>
    public class JoyGiver_HabitRemember : JoyGiver_Habit
    {
        protected override HabitKind Kind => HabitKind.Remember;

        protected override bool Fits(Pawn p) => SmallHabits.FriendWhoLeft(p) != null;

        protected override Job Give(Pawn p)
        {
            var friend = SmallHabits.FriendWhoLeft(p);
            var listener = friend != null ? SmallHabits.ListenerFor(p, friend) : null;
            return listener != null ? Make(listener, LocalTargetInfo.Invalid, new IntRange(700, 1100)) : null;
        }
    }

    /// <summary>Convalescing: the game's own walk, slow and short, much likelier while they're getting back on their feet.</summary>
    public class JoyGiver_HabitSlowWalk : JoyGiver_GoForWalk
    {
        public override float GetChance(Pawn pawn) => SmallHabits.Allowed(pawn, HabitKind.Walk, SmallHabits.Convalescing) ? base.GetChance(pawn) : 0f;

        public override bool CanBeGivenTo(Pawn pawn) => SmallHabits.Allowed(pawn, HabitKind.Walk, SmallHabits.Convalescing) && base.CanBeGivenTo(pawn);

        public override Job TryGiveJob(Pawn pawn)
        {
            if (!SmallHabits.Allowed(pawn, HabitKind.Walk, SmallHabits.Convalescing)) return null;
            var job = base.TryGiveJob(pawn);
            if (job != null) SmallHabits.Done(pawn, HabitKind.Walk, Find.TickManager.TicksGame);
            return job;
        }

        public override Job TryGiveJobInGatheringArea(Pawn pawn, IntVec3 gatherSpot, float maxRadius = -1f) => null;
    }

    /// <summary>A child, in the two days after the first snow: out building a snowman (the game's own), much likelier than usual.</summary>
    public class JoyGiver_HabitSnowPlay : JoyGiver_BuildSnowman
    {
        private static bool Fits(Pawn p) => p.DevelopmentalStage.Child() && WeatherTalk.SnowPlay(p.Map);

        public override float GetChance(Pawn pawn) => SmallHabits.Allowed(pawn, HabitKind.Snow, Fits) ? base.GetChance(pawn) : 0f;

        public override bool CanBeGivenTo(Pawn pawn) => SmallHabits.Allowed(pawn, HabitKind.Snow, Fits) && base.CanBeGivenTo(pawn);

        public override Job TryGiveJob(Pawn pawn)
        {
            if (!SmallHabits.Allowed(pawn, HabitKind.Snow, Fits)) return null;
            var job = base.TryGiveJob(pawn);
            if (job != null) SmallHabits.Done(pawn, HabitKind.Snow, Find.TickManager.TicksGame);
            return job;
        }

        public override Job TryGiveJobInGatheringArea(Pawn pawn, IntVec3 gatherSpot, float maxRadius = -1f) => null;
    }

    // ---------------- The job ----------------

    /// <summary>
    /// A small habit's while somewhere: go there (or to whoever they'll talk to), say something on arriving, and stay a
    /// while facing the grave, the fire, the land or the listener, gaining recreation as they do. At a grave after dark, a
    /// low glow while they stand there. Ends at any danger, when the setting goes off, when their joy is full, when the
    /// fire goes out or the listener walks off, and like any job at the player's word.
    /// </summary>
    public class JobDriver_SmallHabit : JobDriver
    {
        private int stayed;
        /// <summary>Whom it's about, by name, for the report (the dead, the friend who left).</summary>
        private string about;

        private HabitExtension Ext => job.def.GetModExtension<HabitExtension>();

        private Pawn Listener => job.targetA.Thing as Pawn;

        /// <summary>Whom they keep company, or the animal they sit with: they stand beside them, on a cell of their own.</summary>
        private Pawn Partner => job.targetB.Thing as Pawn;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            // The spot by the grave or the fire is theirs while they stay, so whoever comes next stands beside them.
            if (!job.targetA.HasThing && job.targetA.IsValid && pawn.Map != null)
                pawn.Map.pawnDestinationReservationManager.Reserve(pawn, job, job.targetA.Cell);
            return true;
        }

        public override void Notify_Starting()
        {
            base.Notify_Starting();
            try
            {
                about = Subject()?.LabelShort;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("small habit start", e);
            }
        }

        /// <summary>Whom it's about: the dead in the grave, or the friend who left.</summary>
        private Pawn Subject()
        {
            switch (Ext?.kind)
            {
                case HabitKind.Grave: return (job.targetB.Thing as Building_Grave)?.Corpse?.InnerPawn;
                case HabitKind.Remember: return SmallHabits.FriendWhoLeft(pawn);
                case HabitKind.Animals:
                case HabitKind.Company: return Partner;
                default: return null;
            }
        }

        public override string GetReport()
        {
            var ext = Ext;
            // Whom it's about may be unknown (a grave whose body was moved): then the job's own words.
            bool needsName = ext?.kind == HabitKind.Grave || ext?.kind == HabitKind.Remember || ext?.kind == HabitKind.Animals || ext?.kind == HabitKind.Company;
            if (ext != null && !(needsName && about.NullOrEmpty()))
            {
                string key = "CC_Habit_Report_" + ext.kind;
                if (key.CanTranslate()) return key.Translate((about ?? "").Named("OTHER")).Resolve();
            }
            return base.GetReport();
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            var ext = Ext;
            this.FailOn(() => !SmallHabits.On || pawn.Map == null || pawn.Map.dangerWatcher.DangerRating != StoryDanger.None);
            if (job.targetA.HasThing)
            {
                this.FailOnDespawnedOrNull(TargetIndex.A);
                this.FailOn(() => !SmallHabits.CanListen(Listener));
            }
            if (job.targetB.HasThing) this.FailOnDespawnedOrNull(TargetIndex.B);
            if (ext?.kind == HabitKind.Fire || ext?.kind == HabitKind.Stories) this.FailOn(() => !Lit(job.targetB.Thing));
            if (ext?.kind == HabitKind.Company) this.FailOn(() => !MoreHabits.StillCompany(Partner));
            if (ext?.kind == HabitKind.Animals) this.FailOn(() => !MoreHabits.Calm(Partner));
            if (job.targetA.HasThing) yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            else yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);
            var stay = ToilMaker.MakeToil("SmallHabit");
            stay.initAction = Arrive;
            stay.tickIntervalAction = Stay;
            stay.defaultCompleteMode = ToilCompleteMode.Delay;
            stay.defaultDuration = Mathf.Max(600, job.count);
            stay.socialMode = ext != null && ext.social ? RandomSocialMode.SuperActive : RandomSocialMode.Off;
            stay.handlingFacing = true;
            yield return stay;
        }

        private static bool Lit(Thing fire)
        {
            var fuel = fire?.TryGetComp<CompRefuelable>();
            return fire != null && fire.Spawned && (fuel == null || fuel.HasFuel);
        }

        private void Arrive()
        {
            try
            {
                var ext = Ext;
                if (ext == null) return;
                SmallHabits.Done(pawn, ext.kind, Find.TickManager.TicksGame);
                var subject = Subject();
                if (ext.kind == HabitKind.Remember && Listener != null && Together.Can(pawn, Listener))
                    pawn.interactions?.TryInteractWith(Listener, InteractionDefOf.DeepTalk);
                if (ext.kind == HabitKind.Company && Partner != null && Together.Can(pawn, Partner))
                    pawn.interactions?.TryInteractWith(Partner, InteractionDefOf.Chitchat);
                SmallHabits.Say(pawn, ext, subject);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("small habit arrive", e);
            }
        }

        private void Stay(int delta)
        {
            stayed += delta;
            var listener = Listener;
            if (listener != null)
            {
                // A short talk: if they wander off, it's over.
                if (!pawn.Position.InHorDistOf(listener.Position, 5f))
                {
                    EndJobWith(JobCondition.Succeeded);
                    return;
                }
                pawn.rotationTracker.FaceTarget(listener);
            }
            else if (Partner != null)
            {
                // Whom they keep company (or the animal) walked off: it's over.
                if (!pawn.Position.InHorDistOf(Partner.Position, 6f))
                {
                    EndJobWith(JobCondition.Succeeded);
                    return;
                }
                pawn.rotationTracker.FaceTarget(Partner);
            }
            else if (job.targetB.IsValid) pawn.rotationTracker.FaceCell(job.targetB.Cell);
            if (Ext?.kind == HabitKind.Grave && job.targetB.HasThing)
                VisualTouches.Candle(pawn.Map, job.targetB.Thing.Position, Find.TickManager.TicksGame + 300);
            if (pawn.needs?.joy != null) JoyUtility.JoyTickCheckEnd(pawn, delta, JoyTickFullJoyAction.EndJob);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref stayed, "stayed");
            Scribe_Values.Look(ref about, "about");
        }
    }
}
