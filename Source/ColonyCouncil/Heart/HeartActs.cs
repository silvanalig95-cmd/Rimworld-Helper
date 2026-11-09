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
    /// <summary>The heart's small acts on the map (Heart/HeartActs.cs), each a job of its own (Defs/Misc/Rimfolk_HeartActs.xml).</summary>
    public enum HeartActKind
    {
        /// <summary>Strained: a while in a chair or by the fire, staring at nothing.</summary>
        Sit,
        /// <summary>Strained: a while just outside the colony, looking out.</summary>
        StareOut,
        /// <summary>Worn: a few minutes outside from ordinary work, then back to it.</summary>
        StepOut,
        /// <summary>Worn, awake at night: pacing up and down indoors.</summary>
        Pace,
        /// <summary>In good heart: staying at the table to talk after a meal.</summary>
        Linger,
        /// <summary>In high spirits: dancing at a gathering.</summary>
        Dance,
        /// <summary>Grief: sitting a while where the one they lost slept.</summary>
        LostBed,
        /// <summary>Fear: out after dark, they go in to be near someone.</summary>
        Indoors,
        /// <summary>Anger: through a door, and it slams behind them.</summary>
        Door,
        /// <summary>Love: sitting a while beside their partner.</summary>
        SitWith,
        /// <summary>Homesick: of an evening, at the colony's fire, talking of home.</summary>
        HomeFire,
        /// <summary>A bad afternoon (Heart/HeartTimeOff.cs): a while in their room, beside their bed, letting it come.</summary>
        Cry
    }

    /// <summary>On a heart act's JobDef: which act it is, whether they talk with whoever is near, whether they sit.</summary>
    public class HeartActExtension : DefModExtension
    {
        public HeartActKind kind;
        public bool social;
        public bool seated;
    }

    /// <summary>
    /// The heart on the map (docs/design/heart-felt.md, points 4 and 6): small acts by how they are, and strong feelings
    /// acting, each played out with a one-sentence line (Rimfolk_Pool_HeartAct, through Speech.Say).
    /// <list type="bullet">
    /// <item>Strained: a while in a chair or by the fire, or just outside the colony, staring at nothing.</item>
    /// <item>Worn (and near breaking, likelier): a few minutes outside from ordinary work; pacing indoors when awake at
    /// night; a short word at someone near.</item>
    /// <item>In good heart: humming at work (two notes over the head); staying at the table to talk after a meal.</item>
    /// <item>In high spirits: singing at work; dancing at a gathering.</item>
    /// <item>Strong feelings: grief visits the grave (the small habit's own visit) or sits where the one lost slept; fear,
    /// out after dark, goes in to be near someone; anger says a sharp word to whoever it is about, or goes through a door
    /// and slams it; love sits beside a partner; homesickness talks of home at the evening's fire (or looks out over the
    /// land, the small habit's own); joy goes to share it with a friend.</item>
    /// </list>
    /// Only when it is safe and they are free: never in danger, drafted, downed, in a mental state, hungry, exhausted, in
    /// need of a doctor, at someone's play-out or (but for the dance) a gathering; in their free time, or at a break in
    /// ordinary work, never interrupting urgent work (a fire, a patient, a prisoner, a child, a hunt). One act a colonist in
    /// sixteen hours at most and one in the colony in three (HeartFeltMath), each by its chance an hour by band and feeling.
    /// Looked at from the heart's own hour (no tick of its own); the cooldowns are session memory, nothing saved.
    /// </summary>
    public static class HeartActs
    {
        private static CouncilSettings S => ColonyCouncilMod.Settings;

        public const string ActPool = "Rimfolk_Pool_HeartAct";

        /// <summary>The acts by band: the setting "Small acts by how they are".</summary>
        public static bool BandsOn => Heart.On && S.heartActs;

        /// <summary>The strong feelings' acts: the setting "Strong feelings act".</summary>
        public static bool FeelingsOn => Heart.On && S.heartFeelingActs;

        private static readonly Dictionary<int, int> lastPawn = new Dictionary<int, int>();
        private static readonly Dictionary<int, int> lastMap = new Dictionary<int, int>();
        private static Game game;

        /// <summary>The beds of the dead, by their id, while the grief is fresh (Patch_FamilyDied's own look at the bed).</summary>
        private static readonly Dictionary<int, (Building_Bed bed, int tick)> lostBeds = new Dictionary<int, (Building_Bed, int)>();

        private static void Session()
        {
            if (game == Current.Game) return;
            game = Current.Game;
            lastPawn.Clear();
            lastMap.Clear();
            lostBeds.Clear();
        }

        /// <summary>A colonist with a room of their own died (Social/Family.cs, Patch_FamilyDied): their bed, for those who grieve them.</summary>
        public static void Notify_Died(Pawn dead, Building_Bed bed)
        {
            if (dead == null || bed == null || !Heart.On) return;
            Session();
            if (lostBeds.Count > 12)
            {
                int oldest = lostBeds.OrderBy(kv => kv.Value.tick).First().Key;
                lostBeds.Remove(oldest);
            }
            lostBeds[dead.thingIDNumber] = (bed, Find.TickManager.TicksGame);
        }

        private sealed class Option
        {
            public float weight;
            public Func<bool> act;
        }

        private static readonly List<Option> options = new List<Option>(8);

        // ---------------- The hour ----------------

        /// <summary>The heart's hour (HeartFelt.AfterHour): perhaps a small act, if they're free and the hours are past.</summary>
        public static void Hour(Pawn p, PawnHeart h, HeartMapContext m, int now)
        {
            if (!BandsOn && !FeelingsOn) return;
            Session();
            if (!Safe(p, m)) return;
            if (lastPawn.TryGetValue(p.thingIDNumber, out int at) && now - at < HeartFeltMath.ActPawnHours * GenDate.TicksPerHour) return;
            int mapId = p.Map.uniqueID;
            if (lastMap.TryGetValue(mapId, out int mat) && now - mat < HeartFeltMath.ActColonyHours * GenDate.TicksPerHour) return;
            var band = BandsOn ? h.Band : StrainBand.Steady;
            var good = BandsOn ? GoodHeart.Band(h) : GoodBand.None;
            var strong = FeelingsOn ? Strong(h) : null;
            float chance = HeartFeltMath.ActChance(band, good, strong?.Share ?? 0f);
            if (chance <= 0f || !Rand.Chance(chance)) return;
            if (TryAny(p, h, band, good, strong))
            {
                lastPawn[p.thingIDNumber] = now;
                lastMap[mapId] = now;
            }
        }

        /// <summary>Gathers what fits them now and tries them by their weights until one plays. True when one did.</summary>
        public static bool TryAny(Pawn p, PawnHeart h, StrainBand band, GoodBand good, FeelingNow strong)
        {
            options.Clear();
            try
            {
                Gather(p, h, band, good, strong);
                while (options.Count > 0)
                {
                    var o = options.RandomElementByWeight(x => x.weight);
                    options.Remove(o);
                    if (o.act()) return true;
                }
                return false;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the heart's acts", e);
                return false;
            }
            finally
            {
                options.Clear();
            }
        }

        private static void Add(float weight, Func<bool> act) => options.Add(new Option { weight = weight, act = act });

        private static void Gather(Pawn p, PawnHeart h, StrainBand band, GoodBand good, FeelingNow strong)
        {
            string bandTag = "band:" + band.ToString().ToLowerInvariant();
            if (band >= StrainBand.Strained)
            {
                Add(1f, () => Sit(p, bandTag, null));
                Add(0.7f, () => StareOut(p, bandTag));
            }
            if (band >= StrainBand.Worn)
            {
                Add(1f, () => StepOut(p, bandTag));
                Add(0.9f, () => Pace(p, bandTag));
                Add(0.6f, () => SharpWord(p, bandTag, null));
            }
            if (good >= GoodBand.GoodHeart)
            {
                Add(1f, () => Hum(p, "act:hum", 2));
                Add(0.8f, () => Linger(p));
            }
            if (good >= GoodBand.HighSpirits)
            {
                Add(1f, () => Hum(p, "act:sing", 4));
                Add(1.2f, () => Dance(p));
            }
            if (strong == null) return;
            string feeling = "feeling:" + strong.def.key;
            switch (strong.def.key)
            {
                case "Grief":
                    Add(1.5f, () => Grieve(p, feeling));
                    break;
                case "Fear":
                    Add(1.5f, () => Indoors(p, feeling));
                    break;
                case "Anger":
                {
                    var target = strong.target != null && Together.Can(p, strong.target) ? strong.target : null;
                    if (target != null) Add(1.5f, () => SharpWord(p, feeling, target));
                    Add(1f, () => Door(p, feeling));
                    break;
                }
                case "Love":
                    Add(1.5f, () => SitWith(p, feeling));
                    break;
                case "Homesick":
                    Add(1.5f, () => HomeFire(p, feeling) || Habit(p, "Council_HabitGiver_LookOut"));
                    break;
                case "Joy":
                    Add(1.5f, () => ShareJoy(p, feeling));
                    break;
            }
        }

        /// <summary>The strongest feeling that acts, if it is strong (HeartFeltMath.StrongFeeling).</summary>
        public static FeelingNow Strong(PawnHeart h)
        {
            FeelingNow best = null;
            foreach (var f in h.feelings)
            {
                if (f?.def == null || f.Share < HeartFeltMath.StrongFeeling || !Acts(f.def.key)) continue;
                if (best == null || f.intensity > best.intensity) best = f;
            }
            return best;
        }

        /// <summary>The feelings that act on the map.</summary>
        public static bool Acts(string key) => key == "Grief" || key == "Fear" || key == "Anger" || key == "Love" || key == "Homesick" || key == "Joy";

        // ---------------- Who may ----------------

        /// <summary>
        /// One of the colony's own on a home map, up and about and safe: not drafted, downed, in a mental state, hungry,
        /// exhausted or in need of a doctor, at nobody's play-out, with no danger on the map.
        /// </summary>
        public static bool Safe(Pawn p, HeartMapContext m)
        {
            if (p == null || !p.Spawned || p.Map == null || !p.Map.IsPlayerHome || p.Dead || p.Downed || p.Drafted || p.InMentalState || !p.Awake()) return false;
            if (!(p.IsFreeColonist || p.IsSlaveOfColony) || p.IsQuestLodger() || p.DevelopmentalStage.Baby() || !People.HasInnerLife(p)) return false;
            if ((m != null && m.threat) || MapFacts.ThreatOn(p.Map) || p.Map.dangerWatcher.DangerRating != StoryDanger.None) return false;
            if (HealthAIUtility.ShouldSeekMedicalRest(p)) return false;
            if (p.needs?.food != null && p.needs.food.CurCategory >= HungerCategory.UrgentlyHungry) return false;
            if (p.needs?.rest != null && p.needs.rest.CurLevel < 0.2f) return false;
            if (!PlayOuts.CanJoin(p) || PlayOutTracker.Instance?.RunningFor(p) != null) return false;
            return true;
        }

        /// <summary>Their free time: nothing the player ordered, at leisure or wandering about, at no gathering.</summary>
        private static bool FreeTime(Pawn p) => p.GetLord() == null && Together.FreeTime(p) && !(p.CurJob?.playerForced ?? false);

        /// <summary>
        /// Ordinary work, that a few minutes away from does no harm: building, growing, mining, cutting, making, cooking,
        /// hauling, cleaning, research. Never a fire, a patient, a prisoner, a child, a hunt or an animal at hand.
        /// </summary>
        private static readonly HashSet<string> OrdinaryWork = new HashSet<string>
        {
            "Construction", "Growing", "Mining", "PlantCutting", "Smithing", "Tailoring", "Art", "Crafting", "Cooking", "Hauling", "Cleaning", "Research"
        };

        private static bool AtOrdinaryWork(Pawn p)
        {
            var job = p.CurJob;
            var type = job?.workGiverDef?.workType;
            return type != null && !job.playerForced && p.GetLord() == null && OrdinaryWork.Contains(type.defName) && p.carryTracker?.CarriedThing == null;
        }

        /// <summary>At work, any but a fire or a patient: humming or singing doesn't stop them.</summary>
        private static bool AtWork(Pawn p)
        {
            var type = p.CurJob?.workGiverDef?.workType;
            if (type == null) return false;
            string n = type.defName;
            return n != "Firefighter" && n != "Doctor" && n != "Patient" && n != "PatientBedRest";
        }

        private static bool Dark(Map map) => GenCelestial.CurCelestialSunGlow(map) < 0.3f;

        private static bool Outside(Pawn p)
        {
            var room = p.GetRoom();
            return room == null || room.PsychologicallyOutdoors;
        }

        // ---------------- The acts ----------------

        private static JobDef Def(HeartActKind kind) => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_HeartAct_" + kind);

        /// <summary>
        /// An act's job, made and noted but not started (the time off's job giver hands it to the game's think tree):
        /// <paramref name="tag"/> is what its line is said for (its band or feeling), <paramref name="lost"/> the one lost;
        /// <paramref name="timeOff"/> keeps it going while their time off runs, whatever the acts' own settings.
        /// </summary>
        public static Job MakeJob(HeartActKind kind, LocalTargetInfo a, LocalTargetInfo b, IntRange ticks, string tag, LocalTargetInfo c = default, string lost = null,
            bool timeOff = false)
        {
            var def = Def(kind);
            if (def == null) return null;
            var job = JobMaker.MakeJob(def, a, b, c);
            job.count = ticks.RandomInRange;
            Notes.Remove(job);
            Notes.Add(job, new ActNote { tag = tag, lost = lost, timeOff = timeOff });
            return job;
        }

        /// <summary>
        /// Starts an act's job: <paramref name="queued"/> after what they're doing now (the meal), else now.
        /// <paramref name="tag"/> is what its line is said for (its band or feeling), <paramref name="lost"/> the one lost.
        /// </summary>
        private static bool Start(Pawn p, HeartActKind kind, LocalTargetInfo a, LocalTargetInfo b, IntRange ticks, string tag, bool queued = false,
            LocalTargetInfo c = default, string lost = null)
        {
            var job = MakeJob(kind, a, b, ticks, tag, c, lost);
            if (job == null) return false;
            if (queued)
            {
                p.jobs.jobQueue.EnqueueFirst(job);
                return true;
            }
            p.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
            return p.CurJob == job;
        }

        /// <summary>What an act's line is said for, kept with its job while it runs (session memory: after a load, the act's own line).</summary>
        public sealed class ActNote
        {
            public string tag, lost;
            /// <summary>Part of their time off (a day off, a bad afternoon): it runs while that does.</summary>
            public bool timeOff;
        }

        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Job, ActNote> Notes = new System.Runtime.CompilerServices.ConditionalWeakTable<Job, ActNote>();

        /// <summary>The act's note for a job, or null.</summary>
        public static ActNote NoteOf(Job job) => job != null && Notes.TryGetValue(job, out var n) ? n : null;

        /// <summary>Strained: a chair or a bench (indoors or by the fire first), or the colony's lit fire, a while.</summary>
        private static bool Sit(Pawn p, string tag, string extra)
        {
            if (!FreeTime(p)) return false;
            var seat = QuirkHabits.SeatFor(p);
            if (seat != null) return Start(p, HeartActKind.Sit, seat.Position, seat, new IntRange(900, 1500), extra ?? tag);
            var fire = PlaySpots.ColonyFire(p.Map, p);
            if (fire == null || !fire.Position.InHorDistOf(p.Position, 50f) || !p.CanReach(fire, PathEndMode.Touch, Danger.None)) return false;
            var cell = SmallHabits.SpotNear(fire.Position, p.Map, p);
            return cell.IsValid && Start(p, HeartActKind.Sit, cell, fire, new IntRange(900, 1500), extra ?? tag);
        }

        /// <summary>Strained: just outside the colony, facing away, in weather fit for it.</summary>
        private static bool StareOut(Pawn p, string tag)
        {
            if (!FreeTime(p) || !JoyUtility.EnjoyableOutsideNow(p)) return false;
            var c = SmallHabits.LookOutSpot(p, out var face);
            return c.IsValid && Start(p, HeartActKind.StareOut, c, face, new IntRange(700, 1200), tag);
        }

        /// <summary>Worn: a few minutes out under the sky from ordinary work, then back to it (the work is picked up again).</summary>
        private static bool StepOut(Pawn p, string tag)
        {
            if (!AtOrdinaryWork(p) || !JoyUtility.EnjoyableOutsideNow(p)) return false;
            var map = p.Map;
            if (!CellFinder.TryFindRandomReachableNearbyCell(p.Position, map, 18f, TraverseParms.For(p, Danger.None), c => !c.Roofed(map) && PlaySpots.Ok(c, map, p), null,
                    out var cell)) return false;
            return Start(p, HeartActKind.StepOut, cell, IntVec3.Invalid, new IntRange(400, 700), tag);
        }

        /// <summary>Worn and awake at night with nothing to do: up and down the room a few times.</summary>
        private static bool Pace(Pawn p, string tag)
        {
            int hour = GenLocalDate.HourOfDay(p.Map);
            if (hour >= 5 && hour < 22) return false;
            var job = p.CurJob;
            if (p.GetLord() != null || (job != null && (job.playerForced || job.workGiverDef != null || job.def == JobDefOf.Ingest))) return false;
            var room = p.GetRoom();
            if (room == null || room.PsychologicallyOutdoors || room.IsPrisonCell) return false;
            var map = p.Map;
            IntVec3 a = IntVec3.Invalid, b = IntVec3.Invalid;
            float best = 0f;
            foreach (var c in room.Cells)
            {
                if (!PlaySpots.Ok(c, map, p) || c.GetEdifice(map) != null) continue;
                float d = c.DistanceTo(p.Position);
                if (!a.IsValid || d < a.DistanceTo(p.Position)) a = c;
            }
            if (!a.IsValid) return false;
            foreach (var c in room.Cells)
            {
                if (c.GetEdifice(map) != null || !PlaySpots.Ok(c, map, p)) continue;
                float d = c.DistanceTo(a);
                if (d > best && d <= 7f)
                {
                    best = d;
                    b = c;
                }
            }
            return b.IsValid && best >= 2.5f && Start(p, HeartActKind.Pace, a, b, new IntRange(0, 0), tag);
        }

        /// <summary>
        /// Worn, or angry with someone: a short word at someone near (whoever the anger is about first), said to their face.
        /// No job: the line is the act.
        /// </summary>
        private static bool SharpWord(Pawn p, string tag, Pawn target)
        {
            var other = target ?? Together.Company(p);
            if (other == null || !Together.Can(p, other)) return false;
            return SayTo(p, other, tag, target != null ? "about" : null, "act:sharp");
        }

        /// <summary>In good heart at work: two notes over the head and a line; in high spirits, singing: four notes.</summary>
        private static bool Hum(Pawn p, string act, int notes)
        {
            if (!AtWork(p)) return false;
            VisualTouches.Notes(p, notes);
            Say(p, null, act, null);
            return true;
        }

        /// <summary>In good heart, eating with someone at the table: they stay on to talk once the meal is done.</summary>
        private static bool Linger(Pawn p)
        {
            if (p.CurJobDef != JobDefOf.Ingest || p.GetLord() != null || p.jobs?.jobQueue == null || p.jobs.jobQueue.Count > 0) return false;
            var room = p.GetRoom();
            if (room == null || room.PsychologicallyOutdoors) return false;
            Pawn mate = null;
            foreach (var o in p.Map.mapPawns.FreeColonistsSpawned)
            {
                if (o == p || o.Downed || !o.Awake() || o.GetRoom() != room || !o.Position.InHorDistOf(p.Position, 6f)) continue;
                if (o.CurJobDef != JobDefOf.Ingest && !SmallHabits.CanListen(o)) continue;
                mate = o;
                break;
            }
            return mate != null && Start(p, HeartActKind.Linger, IntVec3.Invalid, mate, new IntRange(600, 900), "act:linger", true);
        }

        /// <summary>In high spirits at a gathering: a dance where they stand, then back to it.</summary>
        private static bool Dance(Pawn p)
        {
            if (!(p.GetLord()?.LordJob is LordJob_Joinable_Gathering) || p.CurJob == null || p.CurJob.playerForced) return false;
            return Start(p, HeartActKind.Dance, IntVec3.Invalid, IntVec3.Invalid, new IntRange(300, 500), "act:dance");
        }

        /// <summary>
        /// Grief: the small habit's own visit to the grave when it can be had (Immersion/SmallHabits.cs), else a while where
        /// the one they lost slept, else a while sitting, lost in it.
        /// </summary>
        private static bool Grieve(Pawn p, string tag)
        {
            if (!FreeTime(p)) return false;
            if (Habit(p, "Council_HabitGiver_Grave")) return true;
            var dead = SmallHabitsGrief(p, out var bed);
            if (dead != null && bed != null)
            {
                // Beside the bed, in the same room, facing it.
                var room = bed.GetRoom();
                var cell = SmallHabits.SpotNear(bed.Position, p.Map, p, c => c.GetRoom(p.Map) == room);
                if (cell.IsValid) return Start(p, HeartActKind.LostBed, cell, bed, new IntRange(900, 1400), tag, false, default, dead.LabelShort);
            }
            return Sit(p, tag, tag);
        }

        /// <summary>Someone they grieve (a death in the last twenty days) whose own bed is still here, and the bed.</summary>
        private static Pawn SmallHabitsGrief(Pawn p, out Building_Bed bed)
        {
            bed = null;
            var mem = p.needs?.mood?.thoughts?.memories?.Memories;
            if (mem == null) return null;
            foreach (var m in mem)
            {
                if (m?.def == null || m.otherPawn == null || !m.otherPawn.Dead || m.age > GenDate.TicksPerDay * 20 || !Flavor.IsGriefThought(m.def.defName)) continue;
                if (!lostBeds.TryGetValue(m.otherPawn.thingIDNumber, out var lost) || lost.bed == null || !lost.bed.Spawned || lost.bed.Map != p.Map) continue;
                bed = lost.bed;
                return m.otherPawn;
            }
            return null;
        }

        /// <summary>One of the small habits, through its own joy giver (its setting, its cooldown and what fits): true when it began.</summary>
        private static bool Habit(Pawn p, string giverDef)
        {
            if (!SmallHabits.On || !FreeTime(p)) return false;
            var giver = DefDatabase<JoyGiverDef>.GetNamedSilentFail(giverDef);
            var job = giver?.Worker?.TryGiveJob(p);
            if (job == null) return false;
            p.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
            return p.CurJob == job;
        }

        /// <summary>Fear, out after dark: they go in to be near someone of the colony indoors, a while.</summary>
        private static bool Indoors(Pawn p, string tag)
        {
            if (!FreeTime(p) || !Dark(p.Map) || !Outside(p)) return false;
            Pawn best = null;
            float bestD = float.MaxValue;
            foreach (var o in p.Map.mapPawns.FreeColonistsSpawned)
            {
                if (o == p || o.Downed || o.Dead || Outside(o) || !o.Position.InHorDistOf(p.Position, 45f)) continue;
                float d = o.Position.DistanceTo(p.Position);
                if (d < bestD && p.CanReach(o, PathEndMode.Touch, Danger.None))
                {
                    bestD = d;
                    best = o;
                }
            }
            if (best == null) return false;
            var cell = SmallHabits.SpotNear(best.Position, p.Map, p, c => c.Roofed(p.Map));
            return cell.IsValid && Start(p, HeartActKind.Indoors, cell, best, new IntRange(600, 1000), tag);
        }

        /// <summary>Anger, at leisure: through the nearest door they can reach, and it slams behind them.</summary>
        private static bool Door(Pawn p, string tag)
        {
            if (!FreeTime(p)) return false;
            var map = p.Map;
            Building_Door door = null;
            float bestD = float.MaxValue;
            foreach (var b in map.listerBuildings.allBuildingsColonist)
            {
                if (!(b is Building_Door d) || !d.Spawned || d.IsForbidden(p) || !d.Position.InHorDistOf(p.Position, 14f)) continue;
                float dist = d.Position.DistanceTo(p.Position);
                if (dist < bestD && d.PawnCanOpen(p) && p.CanReach(d, PathEndMode.OnCell, Danger.None))
                {
                    bestD = dist;
                    door = d;
                }
            }
            if (door == null) return false;
            // The far side: the cell beyond the door from where they stand.
            IntVec3 beyond = IntVec3.Invalid;
            foreach (var dir in GenAdj.CardinalDirections)
            {
                var c = door.Position + dir;
                var back = door.Position - dir;
                if (!PlaySpots.Ok(c, map, p) || !back.InBounds(map)) continue;
                if (c.DistanceTo(p.Position) > back.DistanceTo(p.Position))
                {
                    beyond = c;
                    break;
                }
            }
            return beyond.IsValid && Start(p, HeartActKind.Door, door.Position, door, new IntRange(60, 90), tag, false, beyond);
        }

        /// <summary>Love: their partner at leisure or resting in bed, and a while beside them.</summary>
        private static bool SitWith(Pawn p, string tag)
        {
            if (!FreeTime(p)) return false;
            var o = LovePartnerRelationUtility.ExistingMostLikedLovePartner(p, false);
            if (o == null || !o.Spawned || o.Map != p.Map || o.Dead || (o.Downed && !o.InBed()) || o.InMentalState) return false;
            if (!(MoreHabits.AtLeisure(o) || (o.InBed() && o.Awake()))) return false;
            if (!p.CanReach(o, PathEndMode.Touch, Danger.None)) return false;
            var cell = SmallHabits.SpotNear(o.Position, p.Map, p);
            return cell.IsValid && Start(p, HeartActKind.SitWith, cell, o, new IntRange(900, 1400), tag);
        }

        /// <summary>Homesick, of an evening: by the colony's lit fire, talking of home with whoever is there.</summary>
        private static bool HomeFire(Pawn p, string tag)
        {
            if (!FreeTime(p)) return false;
            int hour = GenLocalDate.HourOfDay(p.Map);
            if (hour < 17 || hour > 23) return false;
            var fire = PlaySpots.ColonyFire(p.Map, p);
            if (fire == null || fire.IsForbidden(p) || !fire.Position.InHorDistOf(p.Position, 80f) || !p.CanReach(fire, PathEndMode.Touch, Danger.None)) return false;
            var cell = SmallHabits.SpotNear(fire.Position, p.Map, p);
            return cell.IsValid && Start(p, HeartActKind.HomeFire, cell, fire, new IntRange(1000, 1600), tag);
        }

        /// <summary>
        /// Joy: they go to share it with a friend (their opinion 20 or more), face to face, when the two are together (one
        /// walks over in their free time, Together.When): the line, and a chat between them.
        /// </summary>
        private static bool ShareJoy(Pawn p, string tag)
        {
            if (!FreeTime(p) || p.relations == null) return false;
            Pawn friend = null;
            float best = 19f;
            foreach (var o in p.Map.mapPawns.FreeColonistsSpawned)
            {
                if (o == p || !Together.Able(o) || o.DevelopmentalStage.Baby()) continue;
                float op = p.relations.OpinionOf(o) + Rand.Range(0f, 10f);
                if (op > best && p.CanReach(o, PathEndMode.Touch, Danger.None))
                {
                    best = op;
                    friend = o;
                }
            }
            if (friend == null) return false;
            string key = "heart-joy|" + p.thingIDNumber + "|" + friend.thingIDNumber;
            Together.When(key, p, friend, () =>
            {
                if (!Together.Can(p, friend)) return;
                SayTo(p, friend, tag, null, "act:sharejoy");
                p.interactions?.TryInteractWith(friend, InteractionDefOf.Chitchat);
            }, 6f, 0f);
            return true;
        }

        // ---------------- Time off (Heart/HeartTimeOff.cs): the jobs its giver hands the think tree ----------------

        /// <summary>A bad afternoon: a while beside their own bed in their room, else on a quiet seat, letting it come.</summary>
        public static Job CryJob(Pawn p, string tag)
        {
            var map = p.Map;
            var bed = p.ownership?.OwnedBed;
            if (bed != null && bed.Spawned && bed.Map == map && !bed.IsForbidden(p) && p.CanReach(bed, PathEndMode.Touch, Danger.None))
            {
                var room = bed.GetRoom();
                if (room != null && !room.PsychologicallyOutdoors)
                {
                    var cell = SmallHabits.SpotNear(bed.Position, map, p, c => c.GetRoom(map) == room);
                    if (cell.IsValid) return MakeJob(HeartActKind.Cry, cell, bed, new IntRange(1200, 1800), tag, default, null, true);
                }
            }
            var seat = QuirkHabits.SeatFor(p);
            return seat != null ? MakeJob(HeartActKind.Cry, seat.Position, seat, new IntRange(1200, 1800), tag, default, null, true) : null;
        }

        /// <summary>Out under the sky a while, looking out over the land (in weather fit for it).</summary>
        public static Job LookOutJob(Pawn p, string tag)
        {
            if (!JoyUtility.EnjoyableOutsideNow(p)) return null;
            var c = SmallHabits.LookOutSpot(p, out var face);
            return c.IsValid ? MakeJob(HeartActKind.StareOut, c, face, new IntRange(900, 1400), tag, default, null, true) : null;
        }

        /// <summary>A walk: the game's own, else a while looking out.</summary>
        public static Job WalkJob(Pawn p, string tag)
        {
            if (!JoyUtility.EnjoyableOutsideNow(p)) return null;
            Job job = null;
            try
            {
                job = DefDatabase<JoyGiverDef>.GetNamedSilentFail("GoForWalk")?.Worker?.TryGiveJob(p);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the heart's time off: a walk", e);
            }
            return job ?? LookOutJob(p, tag);
        }

        /// <summary>
        /// A drink, if they drink: beer, wine, mead or any mod's alcohol from the stores or lying about at home, that their
        /// drug policy allows for fun, their faith and nature allow, and that they can reach. Never for someone addicted or
        /// just clean of a drug.
        /// </summary>
        public static Job DrinkJob(Pawn p)
        {
            var map = p.Map;
            if (map == null || AddictionUtility.AddictedToAnything(p) || LifeNewTracker.JustClean(p)) return null;
            Thing best = null;
            float bestD = float.MaxValue;
            foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.Drug))
            {
                if (t == null || !t.Spawned || !IsAlcohol(t.def)) continue;
                if (!t.IsInAnyStorage() && !map.areaManager.Home[t.Position]) continue;
                float d = t.Position.DistanceToSquared(p.Position);
                if (d >= bestD || t.IsForbidden(p) || !FirstPot.WillDrink(p, t.def) || Beliefs.MindsEating(p, t)) continue;
                if (!p.CanReserve(t, 10, 1) || !p.CanReach(t, PathEndMode.ClosestTouch, Danger.None)) continue;
                best = t;
                bestD = d;
            }
            if (best == null) return null;
            var job = JobMaker.MakeJob(JobDefOf.Ingest, best);
            job.count = 1;
            return job;
        }

        /// <summary>A social drug that is alcohol (any mod's, by its chemical).</summary>
        public static bool IsAlcohol(ThingDef d)
        {
            if (d == null || !d.IsDrug || d.ingestible?.drugCategory != DrugCategory.Social) return false;
            var drug = d.GetCompProperties<CompProperties_Drug>();
            return drug?.chemical != null && drug.chemical == ChemicalDefOf.Alcohol;
        }

        /// <summary>The small habit's own visit to a grave (its setting, its cooldown and what fits), as a job; null when none.</summary>
        public static Job GraveJob(Pawn p)
        {
            if (!SmallHabits.On) return null;
            try
            {
                return DefDatabase<JoyGiverDef>.GetNamedSilentFail("Council_HabitGiver_Grave")?.Worker?.TryGiveJob(p);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the heart's time off: the grave", e);
                return null;
            }
        }

        /// <summary>A while beside the bed where someone they grieve slept, if it's still here.</summary>
        public static Job LostBedJob(Pawn p, string tag)
        {
            var dead = SmallHabitsGrief(p, out var bed);
            if (dead == null || bed == null) return null;
            var room = bed.GetRoom();
            var cell = SmallHabits.SpotNear(bed.Position, p.Map, p, c => c.GetRoom(p.Map) == room);
            return cell.IsValid ? MakeJob(HeartActKind.LostBed, cell, bed, new IntRange(900, 1400), tag, default, dead.LabelShort, true) : null;
        }

        /// <summary>A while beside their partner, at leisure or resting in bed.</summary>
        public static Job SitWithJob(Pawn p, string tag)
        {
            var o = LovePartnerRelationUtility.ExistingMostLikedLovePartner(p, false);
            if (o == null || !o.Spawned || o.Map != p.Map || o.Dead || (o.Downed && !o.InBed()) || o.InMentalState) return null;
            if (!(MoreHabits.AtLeisure(o) || (o.InBed() && o.Awake())) || !p.CanReach(o, PathEndMode.Touch, Danger.None)) return null;
            var cell = SmallHabits.SpotNear(o.Position, p.Map, p);
            return cell.IsValid ? MakeJob(HeartActKind.SitWith, cell, o, new IntRange(1200, 1800), tag, default, null, true) : null;
        }

        /// <summary>Of an evening by the colony's lit fire, else a while on a seat or at the fire.</summary>
        public static Job FireJob(Pawn p, string tag)
        {
            var map = p.Map;
            int hour = GenLocalDate.HourOfDay(map);
            var fire = PlaySpots.ColonyFire(map, p);
            if (fire != null && !fire.IsForbidden(p) && fire.Position.InHorDistOf(p.Position, 80f) && p.CanReach(fire, PathEndMode.Touch, Danger.None))
            {
                var cell = SmallHabits.SpotNear(fire.Position, map, p);
                if (cell.IsValid) return MakeJob(hour >= 17 ? HeartActKind.HomeFire : HeartActKind.Sit, cell, fire, new IntRange(1000, 1600), tag, default, null, true);
            }
            var seat = QuirkHabits.SeatFor(p);
            return seat != null ? MakeJob(HeartActKind.Sit, seat.Position, seat, new IntRange(900, 1500), tag, default, null, true) : null;
        }

        /// <summary>
        /// Time with a friend (a partner, kin or a friend by opinion, the closest first): one walks over to the other in their
        /// free time (Together.When) for a real talk, the game's own deep talk. True when it was set going.
        /// </summary>
        public static bool FriendVisit(Pawn p, Pawn with = null)
        {
            if (p?.Map == null || p.relations == null) return false;
            Pawn friend = with;
            if (friend == null)
            {
                float best = 0f;
                foreach (var o in p.Map.mapPawns.FreeColonistsSpawned)
                {
                    if (o == p || !Together.Able(o) || o.DevelopmentalStage.Baby()) continue;
                    float c = HeartAdvice.ClosenessOf(p, o) + Rand.Range(0f, 0.2f);
                    if (c > best && p.CanReach(o, PathEndMode.Touch, Danger.None))
                    {
                        best = c;
                        friend = o;
                    }
                }
            }
            if (friend == null) return false;
            var pair = friend;
            string key = "heart-friend|" + p.thingIDNumber + "|" + pair.thingIDNumber;
            if (Together.Waiting(key)) return true;
            // Now if the two are together, else when they meet (one walks over in their free time) within half a day.
            Together.When(key, p, pair, () =>
            {
                if (!Together.Can(p, pair)) return;
                Say(p, pair, "act:friend", null);
                p.interactions?.TryInteractWith(pair, InteractionDefOf.DeepTalk);
            }, 12f, 0f);
            return true;
        }

        // ---------------- Their line ----------------

        /// <summary>
        /// The act's line (Rimfolk_Pool_HeartAct): tagged [act:...] for the act and [band:...] or [feeling:...] for why;
        /// {OTHER} whom it is with or about, {LOST} the one lost. Said over their head, or to <paramref name="other"/>'s
        /// face; it pushes back their next ambient line (VisualTouches). True when it showed.
        /// </summary>
        public static bool Say(Pawn p, Pawn other, string act, string tag, string lost = null)
        {
            var pool = DefDatabase<FlavorPoolDef>.GetNamedSilentFail(ActPool);
            if (pool?.lines == null || p == null || !p.Spawned) return false;
            if (p.health?.capacities != null && !p.health.capacities.CapableOf(PawnCapacityDefOf.Talking)) return false;
            var tags = Flavor.TagsFor(p);
            tags.Add(act);
            if (!tag.NullOrEmpty()) tags.Add(tag);
            if (other != null) tags.Add("with");
            if (!lost.NullOrEmpty()) tags.Add("lost");
            if (!Flavor.AnyFits(pool.lines.AllLines(), tags)) return false;
            string raw = pool.lines.Pick(ToneUtility.ToneFor(p), tags, p);
            if (raw.NullOrEmpty()) return false;
            string line = raw.Formatted(p.Named("PAWN"), (other ?? p).Named("OTHER"), (lost ?? "").Named("LOST")).Resolve().CapitalizeFirst();
            // together: a line said to someone shows only when the two are really together (Speech.Say checks), and the
            // acts that say one have brought them side by side (a partner's side, the table, a friend walked over to).
            bool said = other != null ? Speech.Say(p, line, other) : Speech.Say(p, line);
            if (said) VisualTouches.Instance?.Spoke(p, Find.TickManager.TicksGame + GenDate.TicksPerDay / 2);
            return said;
        }

        /// <summary>A line said to someone's face: for its band or feeling, else the act's own.</summary>
        private static bool SayTo(Pawn p, Pawn other, string tag, string extra, string act) => Say(p, other, act, tag) || Say(p, other, act, extra);

    }
}
