using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    // Six personal stories of the colony's own life (the content update's variety, the player's go of 20 November 2026;
    // Defs/ArcDefs/Rimfolk_Arcs_ColonyLife.xml), each starting from something Rimfolk already knows and moving on only as
    // things happen on the map: a custom of their own (the calendar), a promise waited on (the wishes), ready for the seat
    // (the seats and their life goals), a prayer for a friend (the gods as the colonist believes them), an unexpected gift
    // (the giving day), the year's aim (the year's first supper and the life goals).

    /// <summary>
    /// A custom of their own: someone whose custom (they asked for it the first time the colony kept it) is on the calendar and
    /// not yet a tradition. It moves on as the colony keeps it again the next year, and ends well when it becomes a tradition;
    /// let pass, it comes to nothing ([letpass]). {NAME} the custom; the tag holds the years kept when the stage began.
    /// </summary>
    public class ArcWorker_CustomOfTheirOwn : ArcWorker
    {
        private static CalendarDay DayOf(string defName, Map map) =>
            defName == null ? null : ColonyCalendar.For(map)?.CustomOf(DefDatabase<DilemmaDef>.GetNamedSilentFail(defName));

        public override ArcCandidate Candidate(Pawn p, List<Pawn> home)
        {
            var book = LifeVarietyBook.Instance;
            var cal = ColonyCalendar.For(p.MapHeld);
            if (book == null || cal == null || !ColonyCalendar.On || p.DevelopmentalStage.Juvenile()) return null;
            foreach (var d in cal.days.Where(d => d != null && d.Custom && d.CustomDef != null))
            {
                if (book.FirstAskerId(d.CustomDef) != p.ThingID || !ColonyStoryMath.CustomStillGrowing(d.keptYears, d.tradition)) continue;
                return new ArcCandidate { key = d.CustomDef.defName, name = d.label, tag = d.keptYears.ToString(), weight = 1.2f };
            }
            return null;
        }

        public override bool CustomGate(Arc arc, ArcGate gate)
        {
            var d = DayOf(arc.key, arc.pawn?.MapHeld);
            if (d == null) return false;
            if (arc.stage >= arc.def.LastStage - 1) return d.tradition;
            return int.TryParse(arc.tag, out int before) && d.keptYears > before;
        }

        public override void Settle(Arc arc)
        {
            var d = DayOf(arc.key, arc.pawn?.MapHeld);
            if (d != null) arc.tag = d.keptYears.ToString();
        }

        public override string Broken(Arc arc) => arc.stage < arc.def.LastStage && DayOf(arc.key, arc.pawn?.MapHeld) == null ? "letpass" : null;

        public override bool PausesAway => false;
    }

    /// <summary>
    /// A promise waited on: the council promised someone their wish four days ago or more and it is still open. Ends well when
    /// that wish is met; let lapse, it comes to nothing ([lapsed]). {NAME} the wish; the key its number.
    /// </summary>
    public class ArcWorker_PromiseWaited : ArcWorker
    {
        private static Wish WishOf(Arc arc) =>
            int.TryParse(arc.key, out int id) ? CouncilGameComponent.Instance?.wishes.FirstOrDefault(w => w.id == id) : null;

        public override ArcCandidate Candidate(Pawn p, List<Pawn> home)
        {
            var comp = CouncilGameComponent.Instance;
            if (comp == null || !ColonyCouncilMod.Settings.enableWishes) return null;
            int now = Find.TickManager.TicksGame;
            var w = comp.wishes.FirstOrDefault(x => x?.def != null && x.pawn == p && x.state == WishState.Open && x.promised
                                                    && now - x.createdTick >= ColonyStoryMath.PromiseWaitDays * GenDate.TicksPerDay);
            if (w == null) return null;
            return new ArcCandidate { key = w.id.ToString(), name = w.LabelInline, weight = 1.1f };
        }

        public override bool CustomGate(Arc arc, ArcGate gate) => WishOf(arc)?.state == WishState.Granted;

        public override string Broken(Arc arc)
        {
            if (arc.stage >= arc.def.LastStage) return null;
            var w = WishOf(arc);
            if (w == null) return "lapsed";
            return w.state == WishState.Open || w.state == WishState.Granted ? null : "lapsed";
        }
    }

    /// <summary>
    /// Ready for the seat: someone whose life goal is a seat on the council that is open to the colony, and who doesn't hold it.
    /// They practise the seat's work, stand for it at an election, and the story ends well when they hold it; an election over
    /// without it comes to nothing ([lost]). {NAME} the seat in the colony's age; the key the seat; its skill the seat's first.
    /// </summary>
    public class ArcWorker_ReadyForTheSeat : ArcWorker
    {
        private static OfficeDef Seat(Arc arc) => DefDatabase<OfficeDef>.GetNamedSilentFail(arc.key);

        public override ArcCandidate Candidate(Pawn p, List<Pawn> home)
        {
            var comp = CouncilGameComponent.Instance;
            if (comp == null || !ColonyCouncilMod.Settings.offices || p.DevelopmentalStage.Juvenile()) return null;
            var goal = comp.aspirations.Active(p);
            var office = goal?.def?.office;
            if (office == null || goal.def.workerClass == null || !typeof(AspirationWorker_Office).IsAssignableFrom(goal.def.workerClass)) return null;
            if (!office.Available || OfficeTracker.HeldBy(p) == office) return null;
            var skill = office.skills?.FirstOrDefault() ?? SkillDefOf.Social;
            return new ArcCandidate { key = office.defName, name = office.Name, skill = skill, weight = 1.1f };
        }

        private static bool Standing(Arc arc)
        {
            var e = OfficeTracker.For(arc.pawn?.MapHeld)?.election;
            return e != null && e.races.Any(r => r.office?.defName == arc.key && r.EntryOf(arc.pawn) != null);
        }

        private static bool Holds(Arc arc) => arc.pawn != null && OfficeTracker.HeldBy(arc.pawn)?.defName == arc.key;

        public override bool CustomGate(Arc arc, ArcGate gate) => arc.stage >= arc.def.LastStage - 1 ? Holds(arc) : Standing(arc) || Holds(arc);

        public override string Broken(Arc arc)
        {
            if (arc.stage >= arc.def.LastStage) return null;
            if (Seat(arc) == null) return ArcTracker.Retired;
            // Standing at an election that is now over, without the seat.
            if (arc.stage == arc.def.LastStage - 1 && !Standing(arc) && !Holds(arc)) return "lost";
            return null;
        }
    }

    /// <summary>
    /// A prayer for a friend: someone who holds to a god, with a shrine of it at home, and someone close to them laid up ill or
    /// badly hurt. They sit with the friend; when the friend is well again they take it as their god's answer (their belief,
    /// nothing more: no line says the god did it) and give thanks at the shrine. The friend's death ends it ([otherdead]).
    /// {NAME} the god as they call it.
    /// </summary>
    public class ArcWorker_PrayerForAFriend : ArcWorker
    {
        private static bool Unwell(Pawn o) => o != null && !o.Dead && (o.Downed || LifeChecks.HasIllness(o, null) || (o.InBed() && LifeChecks.Healing(o)));

        public override ArcCandidate Candidate(Pawn p, List<Pawn> home)
        {
            if (!Gods.On || p.DevelopmentalStage.Juvenile()) return null;
            var god = PawnGods.Held(p);
            if (god == null || Shrines.Nearest(p, god) == null) return null;
            var friend = home.Where(o => o != p && Unwell(o) && ArcUtil.Closeness(p, o) >= ColonyStoryMath.FriendClose).OrderByDescending(o => ArcUtil.Closeness(p, o)).FirstOrDefault();
            if (friend == null) return null;
            return new ArcCandidate { other = friend, name = Worship.Called(god, p), weight = 1.2f };
        }

        public override bool CustomGate(Arc arc, ArcGate gate) => arc.other != null && !arc.other.Dead && !Unwell(arc.other);
    }

    /// <summary>The thanks given at the shrine of the god the story's colonist holds to (A), a short while: the end of "a prayer for a friend".</summary>
    public class DilemmaEffect_ShrineThanks : DilemmaEffect_Visit
    {
        public DilemmaEffect_ShrineThanks()
        {
            to = PlaySpot.Place;
            sit = false;
            duration = new FloatRange(0.5f, 1f);
        }

        protected override bool Fill(PlayOut p, DilemmaContext ctx)
        {
            var god = ctx?.a == null ? null : PawnGods.Held(ctx.a);
            var shrine = god == null ? null : Shrines.Nearest(ctx.a, god);
            if (shrine == null || !shrine.Spawned || shrine.Map != ctx.map || !base.Fill(p, ctx)) return false;
            p.place = shrine;
            p.to = PlaySpot.Place;
            return true;
        }
    }

    /// <summary>
    /// An unexpected gift: someone handed a gift on the giving day by someone they thought little of. They go and thank them,
    /// and the story ends well once the two are friends. {NAME} the gift.
    /// </summary>
    public class ArcWorker_UnexpectedGift : ArcWorker
    {
        public override ArcCandidate Candidate(Pawn p, List<Pawn> home)
        {
            var book = LifeVarietyBook.Instance;
            if (book == null || !book.GiftTo(p, out string giverId, out string label)) return null;
            var giver = home.FirstOrDefault(o => o.ThingID == giverId);
            if (giver == null || giver == p || ArcUtil.Opinion(p, giver) >= ColonyStoryMath.StrangerOpinion) return null;
            return new ArcCandidate { other = giver, name = label, weight = 1.5f };
        }
    }

    /// <summary>
    /// The year's aim: someone who said their life goal aloud at this year's first supper, while the year is young. Halfway there
    /// (or after forty days) it moves on, and it ends well when the goal is reached; a goal given up or changed comes to nothing
    /// ([goalgone]). {GOAL} the goal.
    /// </summary>
    public class ArcWorker_YearsAim : ArcWorker
    {
        public override ArcCandidate Candidate(Pawn p, List<Pawn> home)
        {
            var comp = CouncilGameComponent.Instance;
            var map = p.MapHeld;
            if (comp?.life == null || map == null || !ColonyCouncilMod.Settings.aspirations) return null;
            int year = GenLocalDate.Year(map);
            var said = comp.life.aims.LastOrDefault(a => a.pawnId == p.ThingID && ColonyStoryMath.AimFresh(a.year, year, GenLocalDate.DayOfYear(map)));
            var goal = comp.aspirations.Active(p);
            if (said == null || goal?.def == null || goal.def.defName != said.def) return null;
            return new ArcCandidate { key = said.def, goalText = goal.Goal, weight = 1.3f };
        }

        public override bool CustomGate(Arc arc, ArcGate gate)
        {
            var goal = CouncilGameComponent.Instance?.aspirations.Active(arc.pawn);
            return goal?.def != null && goal.def.defName == arc.key && goal.def.Worker.Progress(goal) >= 0.5f;
        }

        public override string Broken(Arc arc)
        {
            if (arc.stage >= arc.def.LastStage) return null;
            var comp = CouncilGameComponent.Instance;
            var goal = comp?.aspirations.Active(arc.pawn);
            if (goal?.def != null && goal.def.defName == arc.key) return null;
            // Reached: the Goal gate moves it on this hour; anything else is a goal given up or changed.
            bool reached = comp?.aspirations.all.Any(x => x.pawn == arc.pawn && x.done && x.def?.defName == arc.key) ?? false;
            return reached ? null : "goalgone";
        }
    }
}
