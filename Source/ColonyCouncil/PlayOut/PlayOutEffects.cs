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
        public override bool ChanceEach => false;

        /// <summary>What it's called on the map ("the victory feast").</summary>
        protected string LabelFor(DilemmaContext ctx) => (!label.NullOrEmpty() ? ctx.Format(label) : DefaultLabel(ctx)).CapitalizeFirst();

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            // With "Answers play out on the map" off, nothing is set going (its moods come at once, as they always did).
            if (!PlayOuts.On || ctx?.map == null) return;
            DescribePlay(ctx, sheet, LabelFor(ctx));
        }

        /// <summary>What plays out: by default its name, when and for how long.</summary>
        protected virtual void DescribePlay(DilemmaContext ctx, ImpactSheet sheet, string label) =>
            sheet.Play("CC_Impact_Play".Translate(label, DilemmaImpact.When(hours), DilemmaImpact.Hours(duration)).Resolve(), label);

        [MustTranslate] public string label;
        public IntRange hours = new IntRange(PlayOutMath.DayFrom, PlayOutMath.DayTo);
        public float waitHours = PlayOutMath.DefaultWaitHours;
        public FloatRange duration = new FloatRange(2f, 3f);
        /// <summary>
        /// The colony's own doing, set from code (a family supper, a camp's show, a birthday): it waits for room in the
        /// colony's week (<see cref="PlayOut.routine"/>, PlayOut/Stage.cs). An answer the player gave never does.
        /// </summary>
        public bool routine;

        protected abstract PlayKind Kind { get; }

        /// <summary>Who takes part and where; false when it can't happen at all (nobody to do it).</summary>
        protected abstract bool Fill(PlayOut p, DilemmaContext ctx);

        public override void Apply(DilemmaContext ctx) => Create(ctx);

        /// <summary>Sets it going from code (an effect of a personal story that adds to it afterwards): the plan, or null.</summary>
        public PlayOut Begin(DilemmaContext ctx) => Create(ctx);

        /// <summary>Sets it going: the plan, or null when nothing was set going (the setting off, its chance not met, nobody to do it).</summary>
        protected virtual PlayOut Create(DilemmaContext ctx)
        {
            if (!PlayOuts.On || ctx?.map == null) return null;
            if (chance < 1f && !Rand.Chance(chance)) return null;
            try
            {
                var tracker = PlayOutTracker.Instance;
                if (tracker == null) return null;
                int now = Find.TickManager.TicksGame;
                var p = new PlayOut
                {
                    kind = Kind,
                    map = ctx.map,
                    created = now,
                    hourFrom = hours.min,
                    hourTo = hours.max,
                    durationTicks = PlayOutMath.DurationTicks(duration.RandomInRange),
                    routine = routine
                };
                p.label = !label.NullOrEmpty() ? ctx.Format(label) : DefaultLabel(ctx);
                if (!Fill(p, ctx)) return null;
                p.deadline = PlayOutMath.Deadline(now, GenLocalDate.HourOfDay(ctx.map), p.hourFrom, p.hourTo, waitHours);
                tracker.Add(p);
                PlayOuts.Register(p);
                return p;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("play out " + GetType().Name, e);
                return null;
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
        protected override void DescribePlay(DilemmaContext ctx, ImpactSheet sheet, string label)
        {
            var who = Everyone(ctx);
            var lead = PlayOuts.Who(organizer, ctx);
            if (lead != null && PlayOuts.OurPerson(lead) && !who.Contains(lead)) who.Add(lead);
            if (who.Count == 0) return;
            string text = "CC_Impact_Gathering".Translate(label, DilemmaImpact.When(hours), DilemmaImpact.Hours(duration), WhoOf(ctx, who)).Resolve();
            if (music) text += "CC_Impact_WithMusic".Translate().Resolve();
            if (!guests.NullOrEmpty()) text += "CC_Impact_WithGuests".Translate().Resolve();
            sheet.Play(text + Extra(ctx), label);
            Costs(ctx, sheet, who);
        }

        /// <summary>More words on what happens there (a fire laid, drinks poured).</summary>
        protected virtual string Extra(DilemmaContext ctx) => "";

        /// <summary>What it takes from the stores (a feast's meals).</summary>
        protected virtual void Costs(DilemmaContext ctx, ImpactSheet sheet, List<Pawn> who)
        {
        }

        public List<PlaySpot> spots;
        public PlayWho organizer = PlayWho.A;
        public bool music;
        public string pool;
        [MustTranslate] public List<string> lines;
        /// <summary>A personal story of three: its third person comes too (a supper for the three of them).</summary>
        public bool third;
        /// <summary>
        /// Guests from outside the colony at it (PlayGuests.cs): "stranger" (the event's visitor and those with them),
        /// "camp" (the camp's people), "kin" (the visitor and their family), "visitors" (the people of the world event's
        /// visits on the map, those who come while it lasts too); at most <see cref="guestCount"/>.
        /// </summary>
        public string guests;
        public int guestCount = 6;

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
            if (third && (ctx as ArcEffectContext)?.third is Pawn t && PlayOuts.OurPerson(t) && !p.invited.Contains(t)) p.invited.Add(t);
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
            p.guestsFrom = guests;
            p.guestCount = guestCount;
            p.situationId = (ctx as SituationContext)?.situationId ?? -1;
            return true;
        }
    }

    /// <summary>
    /// A shared meal, set going by the one feast engine (PlayFeast.cs): the gathering is at the dining table unless
    /// <see cref="DilemmaEffect_Gathering.spots"/> says otherwise, and everyone who comes is sent to eat a cooked meal from
    /// the stores, the finest first (the food is really eaten). Raw food the answer spends on it is cooked into meals for it
    /// beforehand (PlayCook.cs). Not enough meals for everyone: the first to the table eat; none at all: it's a party, and
    /// the message says so. A while in, the host raises a cup (<see cref="toast"/>, with <see cref="toastLine"/>).
    /// </summary>
    public class DilemmaEffect_Feast : DilemmaEffect_Gathering
    {
        /// <summary>Why it's held (an answer unless set from code: a feast with guests, a wedding's, a family's supper).</summary>
        public FeastWhy why = FeastWhy.Answer;
        /// <summary>The host raises a cup a while in; set from code, what they say with it (else the reason's words, else a toast's).</summary>
        public bool toast = true;
        public string toastLine;

        /// <summary>Through the one feast engine, with the answer's own way of naming who comes and where.</summary>
        protected override PlayOut Create(DilemmaContext ctx)
        {
            if (!PlayOuts.On || ctx?.map == null) return null;
            if (chance < 1f && !Rand.Chance(chance)) return null;
            var order = new FeastOrder
            {
                why = why, map = ctx.map, label = !label.NullOrEmpty() ? ctx.Format(label) : DefaultLabel(ctx), hours = hours, waitHours = waitHours,
                duration = duration, routine = routine, toast = toast, toastLine = toastLine
            };
            return PlayFeast.Stage(order, p => Fill(p, ctx));
        }

        protected override void Costs(DilemmaContext ctx, ImpactSheet sheet, List<Pawn> who)
        {
            // Everyone who comes is sent to eat a cooked meal from the stores; raw food the answer spends is cooked for it first.
            var cat = DefDatabase<ThingCategoryDef>.GetNamedSilentFail("FoodMeals");
            int meals = cat == null ? 0 : ctx.map.resourceCounter.GetCountIn(cat);
            sheet.Add(ImpactKind.Goods, -1, "CC_Impact_FeastMeals".Translate(who.Count, meals).Resolve(), "CC_Impact_FeastMealsBrief".Translate(who.Count).Resolve(), "feastmeals");
        }

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
        protected override string Extra(DilemmaContext ctx) => build && !woodPaid ? "CC_Impact_BonfireLaid".Translate().Resolve() : "";

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

        /// <summary>Harm, part 6: a fire laid for it can burn a hand, and on a dry, windy summer night a spark can catch the grass.</summary>
        protected override void Costs(DilemmaContext ctx, ImpactSheet sheet, List<Pawn> who)
        {
            AnswerHurts.Describe(sheet, build && !woodPaid, false);
            Contests.DescribeSpark(sheet, ctx?.map);
        }

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
        protected override string Extra(DilemmaContext ctx) => "CC_Impact_ToastDrinks".Translate().Resolve();

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

        // Words heard, not a toast: no drinks are poured (ToastDrinks.Pour), and the speaker is the organizer.
        protected override string Extra(DilemmaContext ctx)
        {
            var by = PlayOuts.Who(organizer, ctx);
            return by != null && PlayOuts.OurPerson(by) ? "CC_Impact_WordsBy".Translate(by.LabelShort).Resolve() : "";
        }

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
    /// bed); to a grave (the event's own, the grave of the one a personal story grieves, else one of the colony's dead);
    /// to the camp's fire; or on a long walk. A visit to a place on their own (the water, the fields, the gate, the open
    /// air) or to a grave may name others to go to when that one can't be had (<see cref="orTo"/>: no water near the
    /// colony, no fields, no grave; for a grave, a long walk out too).
    /// </summary>
    public class DilemmaEffect_Visit : DilemmaEffect_PlayOut
    {
        protected override void DescribePlay(DilemmaContext ctx, ImpactSheet sheet, string label)
        {
            var v = PlayOuts.Who(who, ctx);
            if (v == null || v.Dead || !(v.RaceProps?.Humanlike ?? false)) return;
            Pawn other = null;
            if (to == PlaySpot.Person || to == PlaySpot.Stranger)
            {
                var w = to == PlaySpot.Stranger ? PlayWho.X : whom == who && who == PlayWho.B ? PlayWho.A : whom;
                other = PlayOuts.Who(w, ctx);
                if (other == null || other == v || !Here(other, ctx)) return;
            }
            string place = other != null ? DilemmaImpact.Spot(PlaySpot.Person, ctx, other) : DilemmaImpact.Spot(to, ctx);
            sheet.Play("CC_Impact_Visit".Translate(label, v.LabelShort, place, DilemmaImpact.When(hours), DilemmaImpact.Hours(duration)).Resolve(), label);
        }

        public PlayWho who = PlayWho.A;
        public PlaySpot to = PlaySpot.Person;
        public PlayWho whom = PlayWho.B;
        public List<PlaySpot> orTo;
        /// <summary>Work they do there, shown with the game's own effect (breaking up a fallen star: Mine).</summary>
        public EffecterDef work;
        /// <summary>A visit to a person whose words have the two sit down together: at a table (or by the fire, <see cref="sitBy"/>), facing each other (PlaySit.cs).</summary>
        public bool sit;
        public PlaySpot sitBy = PlaySpot.Dining;

        /// <summary>The places a visit can go to on its own (not to a person, a grave or the camp, which have their own ways).</summary>
        public static bool OwnPlace(PlaySpot s) =>
            s == PlaySpot.Fields || s == PlaySpot.Water || s == PlaySpot.Gate || s == PlaySpot.Outdoors || s == PlaySpot.Fire || s == PlaySpot.Place;

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
            p.spots = (OwnPlace(to) || to == PlaySpot.Grave) && orTo != null ? orTo.Where(s => Instead(to, s)).ToList() : new List<PlaySpot>();
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
            p.workEffect = work;
            p.sit = sit;
            p.sitBy = sitBy;
            // A personal story that waited for something made: the thing itself, where it stands (the dream made real).
            if (to == PlaySpot.Place && p.place == null && ctx is ArcEffectContext made)
            {
                var t = made.arc?.madeThing;
                if (t == null || t.Destroyed || !t.Spawned || t.Map != ctx.map) return false;
                p.place = t;
            }
            // A personal story about someone lost: their own grave, when the colony buried them here; never someone
            // else's grave, so where they aren't buried here, the place the story allows instead.
            if (to == PlaySpot.Grave && p.place == null && ctx is ArcEffectContext ac && !(ac.arc?.lostName).NullOrEmpty())
            {
                p.place = PlaySpots.GraveNamed(ctx.map, ac.arc.lostName);
                if (p.place == null && p.spots.Count > 0)
                {
                    p.to = p.spots[0];
                    p.spots.RemoveAt(0);
                }
            }
            return true;
        }

        /// <summary>A place a visit may go instead of <paramref name="to"/>: another place on their own; for a grave, a long walk out too.</summary>
        public static bool Instead(PlaySpot to, PlaySpot s) => OwnPlace(s) || (to == PlaySpot.Grave && s == PlaySpot.Walk);

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
        protected override void DescribePlay(DilemmaContext ctx, ImpactSheet sheet, string label)
        {
            var t = PlayOuts.Who(teacher, ctx);
            var s = PlayOuts.Who(student, ctx);
            if (t == null || s == null || t == s || s.Dead || !s.RaceProps.Humanlike || !t.RaceProps.Humanlike) return;
            var sk = skill ?? ctx.skill ?? BestToLearn(t, s);
            if (sk == null) return;
            sheet.Play("CC_Impact_Lesson".Translate(label, s.LabelShort, sk.label, t.LabelShort, DilemmaImpact.When(hours), DilemmaImpact.Hours(duration)).Resolve(), label);
            sheet.Add(ImpactKind.Skill, 1, "CC_Impact_XpRange".Translate(s.LabelShort, (xpPerHour * duration.min).ToString("0"), (xpPerHour * duration.max).ToString("0"), sk.label).Resolve(),
                "CC_Impact_XpBrief".Translate(s.LabelShort, (xpPerHour * duration.Average).ToString("0"), sk.label).Resolve(), "lesson|" + s.thingIDNumber + "|" + sk.defName);
        }

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
        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (!PlayOuts.On || ctx?.map == null) return;
            var who = Reached(ctx).Where(p => PlayOuts.OurPerson(p) && p.Spawned && p.Map == ctx.map).ToList();
            if (who.Count == 0) return;
            string w = WhoOf(ctx, who);
            sheet.Play((sleep ? "CC_Impact_Sleep" : "CC_Impact_Rest").Translate(w).Resolve(), (sleep ? "CC_Impact_SleepBrief" : "CC_Impact_RestBrief").Translate(w).Resolve());
        }

        public bool sleep;

        public override void Apply(DilemmaContext ctx)
        {
            if (!PlayOuts.On || ctx?.map == null) return;
            try
            {
                var who = Targets(ctx).Where(p => PlayOuts.OurPerson(p) && p.Spawned && p.Map == ctx.map).ToList();
                var cal = ColonyCalendar.For(ctx.map);
                if (who.Count == 0 || cal == null) return;
                var given = cal.RestFor(ctx.map, who, sleep);
                if (given.Count == 0) return;
                string names = target == DilemmaTarget.Colony && given.Count > 2 ? "CC_Play_Everyone".Translate().Resolve()
                    : TextUtil.JoinAnd(given.Select(p => p.LabelShort).ToList());
                string text = (sleep ? "CC_Play_Sleep" : "CC_Play_Rest").Translate(names, given.Count).Resolve();
                Tell.Message(NewsKind.People, Importance.Notable, null, text, new LookTargets(given), MessageTypeDefOf.NeutralEvent);
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
    /// predator, and only when someone can hunt with a weapon. With hurts from everyday life on, one head of it may be
    /// game that can turn on its hunters (Harm/HuntsBite.cs), named under the answer and in the marking's message.
    /// </summary>
    public class DilemmaEffect_Hunt : DilemmaEffect
    {
        public override bool ChanceEach => false;

        private static bool AnyHunter(Map map) =>
            map.mapPawns.FreeColonistsSpawned.Any(p => !p.Downed && p.workSettings != null && p.workSettings.WorkIsActive(WorkTypeDefOf.Hunting) && WorkGiver_HunterHunt.HasHuntingWeapon(p));

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (!PlayOuts.On || ctx?.map == null) return;
            if (!AnyHunter(ctx.map)) return;
            string what = ctx.stranger != null && ctx.stranger.RaceProps.Animal ? ctx.stranger.LabelShort : DilemmaImpact.Kinds(kinds, Math.Max(1, count));
            sheet.Play("CC_Impact_Hunt".Translate(what).Resolve(), "CC_Impact_HuntBrief".Translate(what).Resolve());
            HuntsBite.Describe(sheet, WouldTurn(ctx.map, ctx.stranger, kinds, Math.Max(1, count)));
        }

        public int count = 1;
        public string kinds;

        /// <summary>
        /// The hunt's game now: the one animal the answer is about first, whatever it is; then safe game (PlayHunt.Safe: no
        /// predator, insect or dryad, nothing thrumbo-sized or likely to turn, nothing bonded or venerated), the kinds
        /// wanted first, then the best eating; and with hurts from everyday life on, at most one head that can turn.
        /// </summary>
        public static List<Pawn> Game(Map map, Pawn stranger, string kinds, int count, out Pawn turning)
        {
            turning = null;
            var dm = map.designationManager;
            var wanted = kinds.NullOrEmpty() ? null : kinds.Split('|').Select(k => k.Trim()).Where(k => k.Length > 0).ToList();
            bool mayTurn = HuntsBite.On;
            var result = new List<Pawn>();
            if (stranger != null && stranger.Spawned && stranger.Map == map && stranger.RaceProps.Animal && stranger.Faction == null && !stranger.Downed && !stranger.Dead
                && dm.DesignationOn(stranger) == null)
                result.Add(stranger);
            var rest = map.mapPawns.AllPawnsSpawned
                .Where(a => a != stranger && dm.DesignationOn(a) == null && (PlayHunt.Safe(a, map) || mayTurn && HuntsBite.MayTurn(a, map)))
                .OrderByDescending(a => wanted != null && wanted.Contains(a.def.defName))
                .ThenByDescending(a => a.GetStatValue(StatDefOf.MeatAmount));
            result.AddRange(HuntsBite.Pick(rest, map, Math.Max(1, count) - result.Count, mayTurn, out turning));
            return result;
        }

        /// <summary>The head that can turn the hunt would take now, or null.</summary>
        public static Pawn WouldTurn(Map map, Pawn stranger, string kinds, int count)
        {
            if (map == null || !HuntsBite.On) return null;
            try
            {
                Game(map, stranger, kinds, count, out var turning);
                return turning;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("play out hunt's game", e);
                return null;
            }
        }

        public override void Apply(DilemmaContext ctx)
        {
            if (!PlayOuts.On || ctx?.map == null) return;
            if (chance < 1f && !Rand.Chance(chance)) return;
            try
            {
                var map = ctx.map;
                if (!AnyHunter(map)) return;
                var dm = map.designationManager;
                var game = Game(map, ctx.stranger, kinds, Math.Max(1, count), out var turning);
                if (game.Count == 0) return;
                foreach (var a in game) dm.AddDesignation(new Designation(a, DesignationDefOf.Hunt));
                if (turning != null) HuntsBite.Marked(turning);
                Tell.Message(NewsKind.Land, Importance.Notable, null,
                    "CC_Play_Hunt".Translate(TextUtil.JoinAnd(game.Select(a => a.LabelShort).ToList())).Resolve() + HuntsBite.MarkedNote(turning), new LookTargets(game),
                    MessageTypeDefOf.NeutralEvent);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("play out hunt", e);
            }
        }
    }
}
