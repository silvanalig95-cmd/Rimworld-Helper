using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>What an answer sets going on the map.</summary>
    public enum PlayKind
    {
        /// <summary>A party: people gather, talk, eat and drink what's about.</summary>
        Gathering,
        /// <summary>A shared meal: everyone who comes eats a cooked meal from the stores, best first, then stays on.</summary>
        Feast,
        /// <summary>A gathering round a fire, lit or laid for it.</summary>
        Bonfire,
        /// <summary>A short gathering: a toast, a speech, a reading, a few words said.</summary>
        Toast,
        /// <summary>One colonist goes to someone (or to a grave, the camp, a walk) and spends a while there.</summary>
        Visit,
        /// <summary>A student works at a teacher's side and learns by watching.</summary>
        Lesson
    }

    /// <summary>Where a gathering is held, or where a visit goes.</summary>
    public enum PlaySpot
    {
        /// <summary>Around the dining table (the biggest dining room first).</summary>
        Dining,
        /// <summary>The rec room.</summary>
        Rec,
        /// <summary>A campfire or bonfire of the colony's (a bonfire may lay one).</summary>
        Fire,
        /// <summary>In the open near the middle of the colony, when the weather allows.</summary>
        Outdoors,
        /// <summary>A gather spot (a party spot, a campfire, a table).</summary>
        Gather,
        /// <summary>A grave: the event's own, else one of the colony's dead.</summary>
        Grave,
        /// <summary>The camp on the colony's land, at its fire.</summary>
        Camp,
        /// <summary>Around the stranger (X) of the event.</summary>
        Stranger,
        /// <summary>Visits: to a person (see <see cref="DilemmaEffect_Visit.whom"/>).</summary>
        Person,
        /// <summary>Visits: a long walk out under the sky.</summary>
        Walk
    }

    /// <summary>One of the people of a dilemma or an event.</summary>
    public enum PlayWho
    {
        None,
        A,
        B,
        X
    }

    public enum PlayState
    {
        Pending,
        Running,
        Done
    }

    /// <summary>A good memory an answer gave someone, kept until what it describes has happened.</summary>
    public class HeldMood : IExposable
    {
        public Pawn pawn;
        public ThoughtDef thought;
        public Pawn about;
        public string cause;

        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Defs.Look(ref thought, "thought");
            Scribe_References.Look(ref about, "about");
            Scribe_Values.Look(ref cause, "cause");
        }

        /// <summary>The memory, now (if they're still here to have it).</summary>
        public void Give()
        {
            if (pawn == null || pawn.Dead || thought == null) return;
            using (Causes.Push(cause)) CouncilGameComponent.GiveThought(pawn, thought, about == pawn ? null : about, -1f, cause);
        }
    }

    /// <summary>
    /// One answer playing out: who it's for, where it may happen, how long it may wait, and the moods it holds until
    /// it's over. Kept by <see cref="PlayOutTracker"/>; a gathering's lord and a visit's job find it by its id.
    /// </summary>
    public class PlayOut : IExposable
    {
        public int id;
        public PlayKind kind;
        public PlayState state;
        /// <summary>What it's called in messages ("the victory feast").</summary>
        public string label;
        public Map map;
        /// <summary>Gatherings: who leads it (may stand in for someone who can't).</summary>
        public Pawn organizer;
        /// <summary>Visits and lessons: who goes (the student, for a lesson).</summary>
        public Pawn visitor;
        /// <summary>Visits and lessons: who they go to (the teacher); gatherings around a stranger: the stranger.</summary>
        public Pawn other;
        /// <summary>A grave, or another thing the event named.</summary>
        public Thing place;
        public PlaySpot to = PlaySpot.Person;
        public List<Pawn> invited = new List<Pawn>();
        public List<PlaySpot> spots = new List<PlaySpot>();
        public int created;
        public int deadline;
        public int started = -1;
        public int hourFrom = PlayOutMath.DayFrom;
        public int hourTo = PlayOutMath.DayTo;
        public int durationTicks = PlayOutMath.TicksPerHour * 2;
        /// <summary>Notes over heads now and then (a song, a dance).</summary>
        public bool music;
        /// <summary>A short gathering to hear something said: an apology, a reading, a story told (<see cref="DilemmaEffect_Words"/>).</summary>
        public bool words;
        /// <summary>A flavor pool for what's said at it, and lines of its own.</summary>
        public string pool;
        public List<string> lines = new List<string>();
        public SkillDef skill;
        public float xpPerHour;
        /// <summary>A bonfire with no fire to gather at may lay one, from the colony's wood.</summary>
        public bool buildFire;
        /// <summary>The answer already paid the wood for its fire: one laid for it takes none from the stores.</summary>
        public bool firePaid;
        public List<HeldMood> moods = new List<HeldMood>();
        /// <summary>Why it's still waiting, for the message if it never happens.</summary>
        public string waitReason;

        public bool IsGathering => kind == PlayKind.Gathering || kind == PlayKind.Feast || kind == PlayKind.Bonfire || kind == PlayKind.Toast;

        /// <summary>Takes part in it: invited to the gathering, or the one who goes and the one they go to.</summary>
        public bool TakesPart(Pawn p)
        {
            if (p == null) return false;
            if (IsGathering) return p == organizer || invited.Contains(p);
            return p == visitor || p == other;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_Values.Look(ref kind, "kind");
            Scribe_Values.Look(ref state, "state");
            Scribe_Values.Look(ref label, "label");
            Scribe_References.Look(ref map, "map");
            Scribe_References.Look(ref organizer, "organizer");
            Scribe_References.Look(ref visitor, "visitor");
            Scribe_References.Look(ref other, "other");
            Scribe_References.Look(ref place, "place");
            Scribe_Values.Look(ref to, "to", PlaySpot.Person);
            Scribe_Collections.Look(ref invited, "invited", LookMode.Reference);
            Scribe_Collections.Look(ref spots, "spots", LookMode.Value);
            Scribe_Values.Look(ref created, "created");
            Scribe_Values.Look(ref deadline, "deadline");
            Scribe_Values.Look(ref started, "started", -1);
            Scribe_Values.Look(ref hourFrom, "hourFrom", PlayOutMath.DayFrom);
            Scribe_Values.Look(ref hourTo, "hourTo", PlayOutMath.DayTo);
            Scribe_Values.Look(ref durationTicks, "durationTicks", PlayOutMath.TicksPerHour * 2);
            Scribe_Values.Look(ref music, "music");
            Scribe_Values.Look(ref words, "words");
            Scribe_Values.Look(ref pool, "pool");
            Scribe_Collections.Look(ref lines, "lines", LookMode.Value);
            Scribe_Defs.Look(ref skill, "skill");
            Scribe_Values.Look(ref xpPerHour, "xpPerHour");
            Scribe_Values.Look(ref buildFire, "buildFire");
            Scribe_Values.Look(ref firePaid, "firePaid");
            Scribe_Collections.Look(ref moods, "moods", LookMode.Deep);
            Scribe_Values.Look(ref waitReason, "waitReason");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (invited == null) invited = new List<Pawn>();
                invited.RemoveAll(p => p == null);
                if (spots == null) spots = new List<PlaySpot>();
                if (lines == null) lines = new List<string>();
                if (moods == null) moods = new List<HeldMood>();
                moods.RemoveAll(m => m == null || m.pawn == null || m.thought == null);
            }
        }
    }

    /// <summary>
    /// Answers that play out on the map ("Answers play out on the map" in the settings, on by default): when a dilemma's
    /// or an event's answer describes something people do (a feast, a bonfire, a toast, a visit, a lesson, a day off),
    /// its effects can set it going for real, and the good moods the answer gives those taking part are kept until it
    /// has happened. Nothing here pulls anyone away from danger, sleep or a sickbed; a plan that can't happen within a
    /// few hours leaves just its moods, as before. With the setting off, answers work exactly as they always did.
    /// </summary>
    public static class PlayOuts
    {
        public static bool On => ColonyCouncilMod.Settings?.answersPlayOut ?? false;

        // ---------------- Holding an answer's moods ----------------

        private sealed class Scope
        {
            public readonly List<PlayOut> plays = new List<PlayOut>();
        }

        private static readonly List<Scope> scopes = new List<Scope>();

        public readonly struct Collecting : IDisposable
        {
            private readonly int depth;

            public Collecting(int depth)
            {
                this.depth = depth;
            }

            public void Dispose()
            {
                while (scopes.Count >= depth && scopes.Count > 0) scopes.RemoveAt(scopes.Count - 1);
            }
        }

        /// <summary>Wraps the effects of one answer: what they set going holds the good moods given meanwhile.</summary>
        public static Collecting Collect()
        {
            scopes.Add(new Scope());
            return new Collecting(scopes.Count);
        }

        /// <summary>The answer's effects with those that play out first, so the moods after them can be held.</summary>
        public static List<DilemmaEffect> Ordered(List<DilemmaEffect> effects)
        {
            if (effects == null) return new List<DilemmaEffect>();
            bool any = false;
            foreach (var e in effects)
                if (e is DilemmaEffect_PlayOut)
                {
                    any = true;
                    break;
                }
            if (!any) return effects;
            return effects.Where(e => e is DilemmaEffect_PlayOut).Concat(effects.Where(e => !(e is DilemmaEffect_PlayOut))).ToList();
        }

        internal static void Register(PlayOut p)
        {
            if (p != null && scopes.Count > 0) scopes[scopes.Count - 1].plays.Add(p);
        }

        /// <summary>
        /// A good memory the answer gives someone who takes part in what it set going: kept with the plan and given
        /// when it's over (to those who were there), or when it falls through (to everyone it was meant for). False:
        /// give it now.
        /// </summary>
        public static bool Hold(Pawn p, ThoughtDef thought, Pawn about)
        {
            if (scopes.Count == 0 || p == null || thought == null) return false;
            var scope = scopes[scopes.Count - 1];
            if (scope.plays.Count == 0 || !thought.IsMemory || !Positive(thought)) return false;
            var play = scope.plays.FirstOrDefault(x => x.state != PlayState.Done && x.TakesPart(p));
            if (play == null) return false;
            play.moods.Add(new HeldMood { pawn = p, thought = thought, about = about, cause = Causes.Current });
            return true;
        }

        public static bool Positive(ThoughtDef t) => t?.stages != null && t.stages.Count > 0 && t.stages[0] != null && t.stages[0].baseMoodEffect > 0f;

        /// <summary>
        /// Effects outside a dilemma or an event (a decree, an ambition, a calendar day), applied as an answer's are:
        /// the ones that play out first, each on its own so one failing can't stop the rest.
        /// </summary>
        public static void Run(List<DilemmaEffect> effects, DilemmaContext ctx, string context)
        {
            if (effects.NullOrEmpty() || ctx == null) return;
            using (Collect())
            {
                foreach (var e in Ordered(effects))
                {
                    try
                    {
                        e.Apply(ctx);
                    }
                    catch (Exception ex)
                    {
                        CouncilLog.ErrorOnce(context + " effect " + e.GetType().Name, ex);
                    }
                }
            }
        }

        // ---------------- Who can take part ----------------

        /// <summary>One of the colony's own people (not a guest, a prisoner or a lodger), old enough to take part.</summary>
        public static bool OurPerson(Pawn p) =>
            p != null && !p.Dead && p.RaceProps != null && p.RaceProps.Humanlike && p.Faction == Faction.OfPlayer && !p.IsPrisoner && !p.IsQuestLodger()
            && !p.DevelopmentalStage.Baby();

        /// <summary>
        /// Free to be called to something right now: here, up and about, not under arms, not in a mental state, not in a
        /// sickbed or in need of one, not bleeding, starving or exhausted, not caring for or carrying anyone, not doing
        /// something the player ordered, and not part of another gathering, ritual or caravan.
        /// </summary>
        public static bool CanTakePart(Pawn p, Map map)
        {
            if (!OurPerson(p) || !p.Spawned || p.Map != map) return false;
            if (!CanJoin(p)) return false;
            if (!p.Awake()) return false;
            var lord = p.GetLord();
            return lord == null || lord.LordJob is LordJob_PlayOut;
        }

        /// <summary>The part of <see cref="CanTakePart"/> that also holds while they're at it (asked every so often).</summary>
        public static bool CanJoin(Pawn p)
        {
            if (p == null || p.Dead || !p.Spawned || p.Downed || p.Drafted || p.InMentalState) return false;
            if (p.IsPrisoner || p.IsQuestLodger()) return false;
            if (MedicalRest(p)) return false;
            var health = p.health?.hediffSet;
            if (health != null && health.BleedRateTotal > 0f) return false;
            var food = p.needs?.food;
            if (food != null && food.Starving) return false;
            var rest = p.needs?.rest;
            if (rest != null && rest.CurCategory >= RestCategory.Exhausted) return false;
            if (p.CurJob != null && p.CurJob.playerForced) return false;
            if (p.CurJob == null && p.carryTracker?.CarriedThing is Pawn) return false;
            if (MeetingAttendance.UrgentWork(p) != null) return false;
            return true;
        }

        /// <summary>In a sickbed, or ought to be.</summary>
        public static bool MedicalRest(Pawn p)
        {
            if (p.InBed() && (RestUtility.CurrentBed(p)?.Medical ?? false)) return true;
            if (p.CurJobDef == JobDefOf.LayDownResting) return true;
            return HealthAIUtility.ShouldSeekMedicalRest(p);
        }

        /// <summary>Why nobody should be called anywhere right now, or null: a threat, a gathering already going on, half the colony under arms.</summary>
        public static string WhyNotNow(Map map, bool gathering)
        {
            if (map == null || map.dangerWatcher.DangerRating != StoryDanger.None || GenHostility.AnyHostileActiveThreatToPlayer(map, false, false))
                return "CC_Play_Why_Danger".Translate();
            if (!gathering) return null;
            // The council meeting's own rules for when people can sit down together: no other gathering on, not half
            // the colony under arms, nobody bleeding.
            return MeetingAttendance.WhyNotNow(map) != null ? "CC_Play_Why_Gathering".Translate().Resolve() : null;
        }

        /// <summary>The pawn for one of a dilemma's or an event's people.</summary>
        public static Pawn Who(PlayWho who, DilemmaContext ctx)
        {
            if (ctx == null) return null;
            switch (who)
            {
                case PlayWho.A: return ctx.a;
                case PlayWho.B: return ctx.b;
                case PlayWho.X: return ctx.stranger;
                default: return null;
            }
        }

        /// <summary>A colonist ate something (from the meal hook): counted if it was at a feast.</summary>
        public static void Notify_Ate(Pawn p, Thing food)
        {
            if (p == null || food?.def?.ingestible == null) return;
            try
            {
                if (p.GetLord()?.LordJob is LordJob_PlayOut job) job.Notify_Ate(p, food);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("feast meal", e);
            }
        }
    }
}
