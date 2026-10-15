using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    // Effects that make an answer happen on the map. Any dilemma, event, decree or ambition can list them with its
    // other effects (the order doesn't matter: they're applied first). The good moods the same answer gives those taking
    // part are held until it has happened; with the "Answers play out on the map" setting off, they do nothing and the
    // moods come at once, as they always did.

    /// <summary>
    /// The shared part: what it's called (<see cref="label"/>: "the victory feast", with the answer's placeholders), the
    /// hours it may start in (<see cref="hours"/>, 6~22 unless given; 17~23 for an evening, 20~2 past midnight), how long
    /// it may wait beyond that for danger to pass and people to be free (<see cref="waitHours"/>), and how long it lasts
    /// (<see cref="duration"/>, in hours).
    /// </summary>
    public abstract class DilemmaEffect_PlayOut : DilemmaEffect
    {
        [MustTranslate] public string label;
        public IntRange hours = new IntRange(PlayOutMath.DayFrom, PlayOutMath.DayTo);
        public float waitHours = PlayOutMath.DefaultWaitHours;
        public FloatRange duration = new FloatRange(2f, 3f);

        protected abstract PlayKind Kind { get; }

        /// <summary>Who takes part and where; false when it can't happen at all (nobody to do it).</summary>
        protected abstract bool Fill(PlayOut p, DilemmaContext ctx);

        public override void Apply(DilemmaContext ctx)
        {
            if (!PlayOuts.On || ctx?.map == null) return;
            if (chance < 1f && !Rand.Chance(chance)) return;
            try
            {
                var tracker = PlayOutTracker.Instance;
                if (tracker == null) return;
                int now = Find.TickManager.TicksGame;
                var p = new PlayOut
                {
                    kind = Kind,
                    map = ctx.map,
                    created = now,
                    hourFrom = hours.min,
                    hourTo = hours.max,
                    durationTicks = PlayOutMath.DurationTicks(duration.RandomInRange)
                };
                p.label = !label.NullOrEmpty() ? ctx.Format(label) : DefaultLabel(ctx);
                if (!Fill(p, ctx)) return;
                p.deadline = PlayOutMath.Deadline(now, GenLocalDate.HourOfDay(ctx.map), p.hourFrom, p.hourTo, waitHours);
                tracker.Add(p);
                PlayOuts.Register(p);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("play out " + GetType().Name, e);
            }
        }

        /// <summary>A dilemma's tradition name ("the midsummer bonfire"), else a plain word for the kind ("the feast").</summary>
        protected virtual string DefaultLabel(DilemmaContext ctx)
        {
            if (!(ctx.def?.traditionLabel).NullOrEmpty()) return ctx.def.traditionLabel;
            return ("CC_Play_Label_" + Kind).Translate().Resolve();
        }

        /// <summary>Everyone the effect is for (its target), whatever its chance (that was rolled once, for the whole).</summary>
        protected List<Pawn> Everyone(DilemmaContext ctx)
        {
            float was = chance;
            chance = 1f;
            try
            {
                return Targets(ctx).Where(PlayOuts.OurPerson).Distinct().ToList();
            }
            finally
            {
                chance = was;
            }
        }
    }

    /// <summary>
    /// A gathering of those the effect targets (the colony unless given): a party in the first of <see cref="spots"/>
    /// that works (the rec room, the dining table, a gather spot, the open air, unless given), led by
    /// <see cref="organizer"/> (A unless given) or whoever is free. <see cref="music"/> puts notes over heads; what's said
    /// there comes from <see cref="lines"/> (the answer's placeholders work) and the flavor pool <see cref="pool"/>.
    /// </summary>
    public class DilemmaEffect_Gathering : DilemmaEffect_PlayOut
    {
        public List<PlaySpot> spots;
        public PlayWho organizer = PlayWho.A;
        public bool music;
        public string pool;
        [MustTranslate] public List<string> lines;

        public DilemmaEffect_Gathering()
        {
            target = DilemmaTarget.Colony;
        }

        protected override PlayKind Kind => PlayKind.Gathering;

        protected virtual List<PlaySpot> DefaultSpots => new List<PlaySpot> { PlaySpot.Rec, PlaySpot.Dining, PlaySpot.Gather, PlaySpot.Outdoors };

        protected virtual string DefaultPool => music ? "Council_Pool_PlaySong" : "Council_Pool_PlayParty";

        protected override bool Fill(PlayOut p, DilemmaContext ctx)
        {
            p.invited = Everyone(ctx);
            var lead = PlayOuts.Who(organizer, ctx);
            if (lead != null && PlayOuts.OurPerson(lead) && !p.invited.Contains(lead)) p.invited.Add(lead);
            if (p.invited.Count == 0) return false;
            p.organizer = lead != null && p.invited.Contains(lead) ? lead : null;
            p.spots = spots.NullOrEmpty() ? DefaultSpots : spots.ToList();
            p.music = music;
            p.pool = pool ?? DefaultPool;
            p.lines = lines?.Where(l => !l.NullOrEmpty()).Select(ctx.Format).ToList() ?? new List<string>();
            p.other = ctx.stranger;
            p.place = (ctx as HappeningContext)?.place;
            return true;
        }
    }

    /// <summary>
    /// A shared meal: the gathering is at the dining table unless <see cref="DilemmaEffect_Gathering.spots"/> says
    /// otherwise, and everyone who comes is sent to eat a cooked meal from the stores, the finest first (the food is
    /// really eaten). Not enough meals for everyone: the first to the table eat; none at all: it's a party, and the
    /// message says so.
    /// </summary>
    public class DilemmaEffect_Feast : DilemmaEffect_Gathering
    {
        public DilemmaEffect_Feast()
        {
            duration = new FloatRange(2.5f, 3.5f);
        }

        protected override PlayKind Kind => PlayKind.Feast;

        protected override List<PlaySpot> DefaultSpots => new List<PlaySpot> { PlaySpot.Dining, PlaySpot.Rec, PlaySpot.Gather, PlaySpot.Outdoors };

        protected override string DefaultPool => "Council_Pool_PlayFeast";
    }

    /// <summary>
    /// A gathering round a fire: the colony's campfire if it has one, else (with <see cref="build"/>, on by default) one
    /// laid for the evening in the open, if the colony could build a campfire at all, from its wood (none more with
    /// <see cref="woodPaid"/>); it burns down to ash when the gathering is over. No fire to be had: the open air.
    /// Starts in the evening unless given other hours.
    /// </summary>
    public class DilemmaEffect_Bonfire : DilemmaEffect_Gathering
    {
        public bool build = true;
        /// <summary>The answer already takes the wood for the fire (a Consume of WoodLog): a fire laid for it takes none more.</summary>
        public bool woodPaid;

        public DilemmaEffect_Bonfire()
        {
            hours = new IntRange(16, 23);
            duration = new FloatRange(3f, 4f);
        }

        protected override PlayKind Kind => PlayKind.Bonfire;

        protected override List<PlaySpot> DefaultSpots => new List<PlaySpot> { PlaySpot.Fire, PlaySpot.Outdoors, PlaySpot.Gather };

        protected override string DefaultPool => music ? "Council_Pool_PlaySong" : "Council_Pool_PlayFire";

        protected override bool Fill(PlayOut p, DilemmaContext ctx)
        {
            if (!base.Fill(p, ctx)) return false;
            p.buildFire = build;
            p.firePaid = woodPaid;
            return true;
        }
    }

    /// <summary>
    /// A short gathering, an hour or so: a toast, a few words said, a reading, an apology made in front of everyone.
    /// At the dining table unless given.
    /// </summary>
    public class DilemmaEffect_Toast : DilemmaEffect_Gathering
    {
        public DilemmaEffect_Toast()
        {
            duration = new FloatRange(0.75f, 1.25f);
        }

        protected override PlayKind Kind => PlayKind.Toast;

        protected override List<PlaySpot> DefaultSpots => new List<PlaySpot> { PlaySpot.Dining, PlaySpot.Rec, PlaySpot.Gather, PlaySpot.Outdoors };

        protected override string DefaultPool => "Council_Pool_PlayToast";
    }

    /// <summary>
    /// A short gathering to hear something said: an apology made in front of everyone, a letter or a dream read aloud,
    /// a story told, a question answered. The speaker is the organizer (A unless given).
    /// </summary>
    public class DilemmaEffect_Words : DilemmaEffect_Toast
    {
        protected override string DefaultPool => "Council_Pool_PlayWords";

        protected override bool Fill(PlayOut p, DilemmaContext ctx)
        {
            if (!base.Fill(p, ctx)) return false;
            p.words = true;
            return true;
        }

        protected override string DefaultLabel(DilemmaContext ctx) => "CC_Play_Label_Gathering".Translate().Resolve();
    }

    /// <summary>
    /// One colonist (<see cref="who"/>, A unless given) goes somewhere for a while: to a person (<see cref="whom"/>: B
    /// unless given, A or the stranger X), keeping them company wherever they are (at their bedside, if they're in
    /// bed); to a grave (the event's own, else one of the colony's dead); to the camp's fire; or on a long walk.
    /// </summary>
    public class DilemmaEffect_Visit : DilemmaEffect_PlayOut
    {
        public PlayWho who = PlayWho.A;
        public PlaySpot to = PlaySpot.Person;
        public PlayWho whom = PlayWho.B;

        public DilemmaEffect_Visit()
        {
            duration = new FloatRange(1.5f, 2.5f);
        }

        protected override PlayKind Kind => PlayKind.Visit;

        protected override bool Fill(PlayOut p, DilemmaContext ctx)
        {
            // Someone joining in the same answer (a stranger taken in) may not be one of ours yet: checked when it starts.
            var v = PlayOuts.Who(who, ctx);
            if (v == null || v.Dead || v.RaceProps == null || !v.RaceProps.Humanlike) return false;
            p.visitor = v;
            p.to = to;
            if (to == PlaySpot.Person || to == PlaySpot.Stranger)
            {
                // B going to someone means going to A, unless the effect says otherwise.
                var w = to == PlaySpot.Stranger ? PlayWho.X : whom == who && who == PlayWho.B ? PlayWho.A : whom;
                var o = PlayOuts.Who(w, ctx);
                if (o == null || o == v || !Here(o, ctx)) return false;
                p.other = o;
                p.to = PlaySpot.Person;
            }
            p.place = (ctx as HappeningContext)?.place;
            return true;
        }

        protected override string DefaultLabel(DilemmaContext ctx) => ("CC_Play_Label_" + Kind).Translate().Resolve();

        /// <summary>On the map, or about to walk onto it (a stranger the answer takes in): anyone else can't be visited.</summary>
        public static bool Here(Pawn o, DilemmaContext ctx) =>
            o != null && !o.Dead && ((o.Spawned && o.Map == ctx.map) || (o == ctx.stranger && ((ctx as HappeningContext)?.xWaiting ?? false)) || o.Faction == Faction.OfPlayer);
    }

    /// <summary>
    /// A lesson: the student (<see cref="student"/>, A unless given) works at the teacher's side (<see cref="teacher"/>,
    /// B unless given) for a while, following them about their work and learning <see cref="skill"/> by watching
    /// (the dilemma's or event's skill when none is given, else what the teacher is best at that the student can learn),
    /// at <see cref="xpPerHour"/>.
    /// </summary>
    public class DilemmaEffect_Lesson : DilemmaEffect_PlayOut
    {
        public PlayWho teacher = PlayWho.B;
        public PlayWho student = PlayWho.A;
        public SkillDef skill;
        public float xpPerHour = 150f;

        public DilemmaEffect_Lesson()
        {
            duration = new FloatRange(1.5f, 2.5f);
        }

        protected override PlayKind Kind => PlayKind.Lesson;

        protected override bool Fill(PlayOut p, DilemmaContext ctx)
        {
            var t = PlayOuts.Who(teacher, ctx);
            var s = PlayOuts.Who(student, ctx);
            if (t == null || s == null || t == s || s.Dead || !s.RaceProps.Humanlike || !t.RaceProps.Humanlike) return false;
            if (!DilemmaEffect_Visit.Here(t, ctx) || !DilemmaEffect_Visit.Here(s, ctx)) return false;
            p.visitor = s;
            p.other = t;
            p.to = PlaySpot.Person;
            p.skill = skill ?? ctx.skill ?? BestToLearn(t, s);
            p.xpPerHour = xpPerHour;
            return true;
        }

        private static SkillDef BestToLearn(Pawn teacher, Pawn student) =>
            teacher.skills?.skills
                .Where(x => !x.TotallyDisabled && student.skills?.GetSkill(x.def) is SkillRecord r && !r.TotallyDisabled && r.Level < x.Level)
                .OrderByDescending(x => x.Level).FirstOrDefault()?.def;

        protected override string DefaultLabel(DilemmaContext ctx) => ("CC_Play_Label_" + Kind).Translate().Resolve();
    }

    /// <summary>
    /// Time off for the targets (A unless given): the rest of today's work hours become recreation, as on the
    /// calendar's rest day, and go back tomorrow; with <see cref="sleep"/>, bed instead (someone worn out sent to sleep
    /// it off). The industrious keep their hours. Only the timetable changes: danger, sickbeds and the drafted are
    /// the game's as always.
    /// </summary>
    public class DilemmaEffect_Rest : DilemmaEffect
    {
        public bool sleep;

        public override void Apply(DilemmaContext ctx)
        {
            if (!PlayOuts.On || ctx?.map == null) return;
            try
            {
                var who = Targets(ctx).Where(p => PlayOuts.OurPerson(p) && p.Spawned && p.Map == ctx.map).ToList();
                var cal = CalendarTracker.Instance;
                if (who.Count == 0 || cal == null) return;
                var given = cal.RestFor(ctx.map, who, sleep);
                if (given.Count == 0) return;
                string names = target == DilemmaTarget.Colony && given.Count > 2 ? "CC_Play_Everyone".Translate().Resolve()
                    : TextUtil.JoinAnd(given.Select(p => p.LabelShort).ToList());
                string text = (sleep ? "CC_Play_Sleep" : "CC_Play_Rest").Translate(names, given.Count).Resolve();
                Messages.Message(text, new LookTargets(given), MessageTypeDefOf.NeutralEvent, false);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("play out rest", e);
            }
        }
    }

    /// <summary>
    /// The hunters are sent out: up to <see cref="count"/> wild animals on the map marked for hunting, the event's own
    /// (X) if it's an animal, else game of the kinds in <see cref="kinds"/> ("Elk|Deer"), else any good game. Never a
    /// predator or anything likely to turn on the hunters, and only when someone can hunt with a weapon.
    /// </summary>
    public class DilemmaEffect_Hunt : DilemmaEffect
    {
        public int count = 1;
        public string kinds;

        public override void Apply(DilemmaContext ctx)
        {
            if (!PlayOuts.On || ctx?.map == null) return;
            if (chance < 1f && !Rand.Chance(chance)) return;
            try
            {
                var map = ctx.map;
                if (!map.mapPawns.FreeColonistsSpawned.Any(p => !p.Downed && p.workSettings != null && p.workSettings.WorkIsActive(WorkTypeDefOf.Hunting)
                                                               && WorkGiver_HunterHunt.HasHuntingWeapon(p))) return;
                var dm = map.designationManager;
                var wanted = kinds.NullOrEmpty() ? null : kinds.Split('|').Select(k => k.Trim()).Where(k => k.Length > 0).ToList();
                var game = map.mapPawns.AllPawnsSpawned
                    .Where(a => a.RaceProps.Animal && a.Faction == null && !a.Downed && !a.Dead && !a.RaceProps.predator && !a.InMentalState
                                && dm.DesignationOn(a) == null && !a.Position.Fogged(map) && PawnUtility.GetManhunterOnDamageChance(a) <= 0.1f)
                    .OrderByDescending(a => a == ctx.stranger)
                    .ThenByDescending(a => wanted != null && wanted.Contains(a.def.defName))
                    .ThenByDescending(a => a.GetStatValue(StatDefOf.MeatAmount))
                    .Take(Math.Max(1, count)).ToList();
                if (game.Count == 0) return;
                foreach (var a in game) dm.AddDesignation(new Designation(a, DesignationDefOf.Hunt));
                Messages.Message("CC_Play_Hunt".Translate(TextUtil.JoinAnd(game.Select(a => a.LabelShort).ToList())).Resolve(), new LookTargets(game),
                    MessageTypeDefOf.NeutralEvent, false);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("play out hunt", e);
            }
        }
    }
}
