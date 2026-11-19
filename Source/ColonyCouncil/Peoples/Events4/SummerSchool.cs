using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    // ===================== 21. The summer school (the learned society) =====================

    /// <summary>The summer school's days: who came to its lessons and how often, the colony's books on its table, and the fellows' disputation.</summary>
    public class SchoolState : CampWorkState
    {
        public List<Pawn> students = new List<Pawn>();
        public List<int> lessons = new List<int>();
        /// <summary>The books the fellows brought themselves (their ids), so only the colony's go back.</summary>
        public List<int> ownBooks = new List<int>();
        /// <summary>The colony lends its books (asked at the gate), and they are on the table now.</summary>
        public bool lendAsked, lent, ownCounted;
        /// <summary>Lessons given today (and which day that is).</summary>
        public int lessonDay = -1, lessonsToday;
        /// <summary>The fellows' disputation: rolled once a stay; since when it runs (-1: none), and the day it ended in a sulk.</summary>
        public bool disputeRolled, disputeSettled;
        public int disputeSince = -1, sulkDay = -1;

        public int LessonsOf(Pawn p)
        {
            int i = students.IndexOf(p);
            return i >= 0 && i < lessons.Count ? lessons[i] : 0;
        }

        public void Taught(Pawn p)
        {
            int i = students.IndexOf(p);
            if (i < 0)
            {
                students.Add(p);
                lessons.Add(1);
            }
            else if (i < lessons.Count) lessons[i]++;
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref students, "students", LookMode.Reference);
            Scribe_Collections.Look(ref lessons, "lessons", LookMode.Value);
            Scribe_Collections.Look(ref ownBooks, "ownBooks", LookMode.Value);
            Scribe_Values.Look(ref lendAsked, "lendAsked");
            Scribe_Values.Look(ref lent, "lent");
            Scribe_Values.Look(ref ownCounted, "ownCounted");
            Scribe_Values.Look(ref lessonDay, "lessonDay", -1);
            Scribe_Values.Look(ref lessonsToday, "lessonsToday");
            Scribe_Values.Look(ref disputeRolled, "disputeRolled");
            Scribe_Values.Look(ref disputeSettled, "disputeSettled");
            Scribe_Values.Look(ref disputeSince, "disputeSince", -1);
            Scribe_Values.Look(ref sulkDay, "sulkDay", -1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                students ??= new List<Pawn>();
                lessons ??= new List<int>();
                ownBooks ??= new List<int>();
                // A student lost to the save goes with their count.
                for (int i = students.Count - 1; i >= 0; i--)
                    if (students[i] == null)
                    {
                        students.RemoveAt(i);
                        if (i < lessons.Count) lessons.RemoveAt(i);
                    }
                while (lessons.Count < students.Count) lessons.Add(0);
            }
        }
    }

    /// <summary>
    /// The summer school (item 21): in summer, two or three fellows of the society camp for a week and give a lesson each
    /// afternoon at their table. The colony sends one or two of its people a day (Intellectual, Medicine or Social), lends
    /// them its books (more is learned with them on the table, and they come back as the school goes), and feeds them. The
    /// best student goes home with a book, and the society examines them sooner (item 18). It can turn: two fellows fall
    /// into a disputation that the colony's talker can end, or it ends in a sulk. The lessons take the form of the colony's
    /// age: tallies, slates, books, data slates.
    /// </summary>
    public static class SummerSchool
    {
        public static CampKindDef Kind => DefDatabase<CampKindDef>.GetNamedSilentFail("Rimfolk_Camp_SummerSchool");

        public static JobDef SettleJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv4_Settle");

        private static int Now => Find.TickManager.TicksGame;

        public static Camp On(Map map) => CampTracker.VisitorsAt(map).FirstOrDefault(c => c.kind == Kind);

        public static SchoolState StateOf(Camp c) => c?.workState as SchoolState;

        public static bool IsBook(Thing t) => t != null && t.def?.thingClass != null && typeof(Book).IsAssignableFrom(t.def.thingClass) && t.def.defName != "Tome";

        /// <summary>The colony's books in its stores, the kind it has most of first (what it would lend).</summary>
        public static List<Thing> Books(Map map) =>
            map == null ? new List<Thing>() : map.listerThings.AllThings.Where(t => IsBook(t) && t.Spawned && t.IsInAnyStorage() && !t.IsForbidden(Faction.OfPlayer)).ToList();

        /// <summary>The camp made and set going (the calving's way), its people walking in from the edge; <paramref name="lend"/>: the colony's books go to their table once they are settled.</summary>
        public static Camp Start(HappeningContext ctx, bool lend)
        {
            var map = ctx?.map;
            var tracker = CampTracker.Instance;
            var kind = Kind;
            if (map == null || tracker == null || kind == null || ctx.faction == null || !CampTracker.Enabled) return null;
            try
            {
                var usual = CampWeather.StayRange(map, GenDate.TicksPerDay * 7);
                bool hot = CampWeatherMath.PitchForHeat(usual.high);
                if (!CampSite.TryFind(map, out var center, hot)) return null;
                center = CampLand.SiteFor(kind, map, center, hot);
                if (!center.IsValid) return null;
                var setup = tracker.Make(map, center, CampSite.ColonyCenter(map), kind, ctx.faction, hot, null, null);
                if (setup == null) return null;
                setup.land = TravelLand.Read(map);
                var c = tracker.Start(setup);
                if (c == null)
                {
                    CampTracker.Discard(setup);
                    return null;
                }
                c.workState = new SchoolState { lendAsked = lend };
                return c;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 4: the summer school", e);
                return null;
            }
        }

        /// <summary>The colony's books carried out to the school's table (the camps' own carrying), the kind it has most of.</summary>
        public static bool Lend(Camp c, Pawn by)
        {
            var st = StateOf(c);
            if (st == null || st.lent || c.map == null) return false;
            var g = Books(c.map).GroupBy(t => t.def).OrderByDescending(x => x.Count()).FirstOrDefault();
            if (g == null) return false;
            int n = Mathf.Min(PeopleEvents4Math.LendBooks, g.Count());
            CampAnswers.Carry(c, g.Key.defName, n, by, "CC_PEv4_LendLabel".Translate().Resolve());
            st.lent = true;
            st.lendAsked = false;
            return true;
        }

        /// <summary>A lesson given: counted for the student, and for the day.</summary>
        public static void Taught(Camp c, Pawn student)
        {
            var st = StateOf(c);
            if (st == null || student == null) return;
            int day = GenDate.DaysPassed;
            if (st.lessonDay != day)
            {
                st.lessonDay = day;
                st.lessonsToday = 0;
            }
            st.lessonsToday++;
            st.Taught(student);
        }

        public static int LessonsToday(SchoolState st) => st == null || st.lessonDay != GenDate.DaysPassed ? 0 : st.lessonsToday;

        /// <summary>The colony's talker at the fellows' table (their job done): the disputation ended, or a sulk, by the card's odds.</summary>
        public static void Settled(Pawn talker)
        {
            var c = CampTracker.VisitorsAt(talker?.Map).FirstOrDefault(x => x.kind == Kind);
            var st = StateOf(c);
            if (st == null || st.disputeSince < 0) return;
            int social = talker.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0;
            if (Rand.Chance(PeopleEvents4Math.SettleOdds(social)))
            {
                st.disputeSince = -1;
                st.disputeSettled = true;
                c.AddTension(PeopleEvents4Math.SettledTension);
                talker.skills?.Learn(SkillDefOf.Social, 400f, true);
                var fellow = c.Here.FirstOrDefault(p => p.RaceProps.Humanlike);
                if (fellow != null) c.SayMoment(fellow, "settled");
                c.Note(c.Text("CC_PEv4_SchoolSettled", fellow, talker), MessageTypeDefOf.PositiveEvent, Importance.Notable, talker, true);
            }
            else Sulk(c, st, talker);
        }

        /// <summary>The disputation ends badly: a sulk, no more lessons today, the camp's mood soured.</summary>
        public static void Sulk(Camp c, SchoolState st, Pawn talker = null)
        {
            st.disputeSince = -1;
            st.sulkDay = GenDate.DaysPassed;
            c.AddTension(PeopleEvents4Math.SulkTension);
            var fellow = c.Here.FirstOrDefault(p => p.RaceProps.Humanlike);
            if (fellow != null) c.SayMoment(fellow, "sulk");
            c.Note(c.Text(talker != null ? "CC_PEv4_SchoolSulkTried" : "CC_PEv4_SchoolSulk", fellow, talker), MessageTypeDefOf.NeutralEvent, Importance.Notable, fellow, true);
        }
    }

    /// <summary>The summer school's days: the books lent, the afternoon at the table, the disputation, and the best student's book as they go.</summary>
    public class CampWorker_SummerSchool : CampWorker
    {
        private static int Now => Find.TickManager.TicksGame;

        public override void Started(Camp c)
        {
            if (!(c.workState is SchoolState)) c.workState = new SchoolState();
        }

        public override void Hourly(Camp c, int hour)
        {
            if (!(c.workState is SchoolState st))
            {
                Started(c);
                st = SummerSchool.StateOf(c);
            }
            // The books they brought themselves, so only the colony's go back.
            if (!st.ownCounted && c.Settled)
            {
                st.ownCounted = true;
                foreach (var p in c.Here)
                    foreach (var t in p.inventory?.innerContainer?.Where(SummerSchool.IsBook) ?? Enumerable.Empty<Thing>())
                        st.ownBooks.Add(t.thingIDNumber);
                foreach (var t in c.Goods().Where(SummerSchool.IsBook)) st.ownBooks.Add(t.thingIDNumber);
            }
            if (st.lendAsked && st.ownCounted && c.DaysHere >= 0.2f) SummerSchool.Lend(c, null);
            var fellows = c.Here.Where(p => p.RaceProps.Humanlike).ToList();
            // The disputation: once a stay, after the second day, by day; ended by the colony's talker or in a sulk.
            if (st.disputeSince >= 0)
            {
                if (fellows.Count >= 2 && Rand.Chance(0.6f)) c.SayMoment(fellows[Rand.Range(0, 2)], "dispute");
                if (Now - st.disputeSince >= PeopleEvents4Math.DisputeHours * GenDate.TicksPerHour) SummerSchool.Sulk(c, st);
                return;
            }
            if (!st.disputeRolled && c.DaysHere >= 2f && hour >= 10 && hour <= 16 && fellows.Count >= 2)
            {
                st.disputeRolled = true;
                if (Rand.Chance(PeopleEvents4Math.DisputeChance))
                {
                    st.disputeSince = Now;
                    c.SayMoment(fellows[0], "dispute");
                    c.Note(c.Text("CC_PEv4_SchoolDispute", fellows[0], null, 0, fellows[1].LabelShort), MessageTypeDefOf.NeutralEvent, Importance.Notable, fellows[0], true);
                }
            }
        }

        /// <summary>In the afternoon a fellow at their table, with the lesson's slates or books; in a disputation both of them there.</summary>
        public override Job DayJob(Camp c, Pawn p)
        {
            var st = SummerSchool.StateOf(c);
            var job = PeopleEvents4.TeachJob;
            if (st == null || job == null || !p.RaceProps.Humanlike) return null;
            int hour = GenLocalDate.HourOfDay(c.map);
            bool dispute = st.disputeSince >= 0;
            if (!dispute && (!PeopleEvents4Math.LessonHour(hour) || st.sulkDay == GenDate.DaysPassed)) return null;
            var table = c.plans.FirstOrDefault(x => x.role == "table")?.Built(c.map, c.faction);
            if (table == null || !p.CanReach(table, PathEndMode.Touch, Danger.Some)) return null;
            // One teaches at a time; both stand there to argue.
            if (!dispute && c.Here.Any(o => o != p && o.CurJobDef == job)) return null;
            var j = JobMaker.MakeJob(job, table);
            j.count = GenDate.TicksPerHour;
            return j;
        }

        /// <summary>Going: the colony's books set down for it, the best student's book, the society's thanks.</summary>
        public override void Leaving(Camp c)
        {
            var st = SummerSchool.StateOf(c);
            if (st == null || c.map == null) return;
            var at = c.share.IsValid ? c.share : c.FireCell;
            int back = 0;
            foreach (var p in c.Here)
                foreach (var t in p.inventory?.innerContainer?.Where(x => SummerSchool.IsBook(x) && !st.ownBooks.Contains(x.thingIDNumber)).ToList() ?? new List<Thing>())
                    if (p.inventory.innerContainer.TryDrop(t, at, c.map, ThingPlaceMode.Near, out var dropped))
                    {
                        dropped?.SetForbidden(false, false);
                        back++;
                    }
            // And any set down at their camp rather than carried.
            foreach (var t in c.Goods().Where(x => SummerSchool.IsBook(x) && !st.ownBooks.Contains(x.thingIDNumber)).ToList())
            {
                c.Disown(t);
                t.SetForbidden(false, false);
                back++;
            }
            if (back > 0) c.Note(c.Text("CC_PEv4_SchoolBooksBack", null, null, back), MessageTypeDefOf.PositiveEvent, Importance.Notable, null, true);
            // The best student: the most lessons, two at least.
            Pawn best = null;
            int most = 0;
            for (int i = 0; i < st.students.Count; i++)
            {
                var s = st.students[i];
                int n = i < st.lessons.Count ? st.lessons[i] : 0;
                if (s == null || s.Dead || s.Faction != Faction.OfPlayer || n <= most) continue;
                best = s;
                most = n;
            }
            if (best != null && PeopleEvents4Math.BestStudent(most))
            {
                PeopleEvents4Book.Instance?.NoteBestStudent(best);
                var book = Fellowship.MakeBook(true);
                if (book != null && GenPlace.TryPlaceThing(book, at, c.map, ThingPlaceMode.Near, out var placed))
                {
                    placed?.SetForbidden(false, false);
                    c.Note(c.Text("CC_PEv4_SchoolBest", c.leader, best, most, book.LabelNoCount), MessageTypeDefOf.PositiveEvent, Importance.Notable, placed, true);
                }
            }
            if (st.students.Count > 0 && c.faction != null && c.faction.HasGoodwill && !c.faction.IsPlayer && !c.Hostile)
            {
                WorldSituationWorker.Goodwill(c.faction, PeopleEvents4Math.SchoolGoodwill, GoodwillWhy.Helped);
                FactionMemory.Note(c.faction, "campHosted", "CC_PEv4_SchoolName".Translate().Resolve());
            }
        }

        public override void AddMarks(Camp c, HashSet<string> marks)
        {
            var st = SummerSchool.StateOf(c);
            if (st == null) return;
            if (st.students.Count > 0) marks.Add("lessons");
            if (st.lent) marks.Add("books");
            if (st.sulkDay >= 0) marks.Add("sulk");
        }

        public override string AskBlocked(Camp c, CampAsk a)
        {
            var st = SummerSchool.StateOf(c);
            if (st == null) return null;
            switch (a?.key)
            {
                case "lessonInt":
                case "lessonMed":
                case "lessonSoc":
                    if (st.disputeSince >= 0 || st.sulkDay == GenDate.DaysPassed) return "CC_PEv4_NoLessonToday".Translate().Resolve();
                    if (!PeopleEvents4Math.LessonHour(GenLocalDate.HourOfDay(c.map))) return "CC_PEv4_LessonAfternoon".Translate(PeopleEvents4Math.LessonFromHour, PeopleEvents4Math.LessonToHour).Resolve();
                    if (SummerSchool.LessonsToday(st) >= PeopleEvents4Math.LessonsPerDay) return "CC_PEv4_LessonsDone".Translate(PeopleEvents4Math.LessonsPerDay).Resolve();
                    return null;
                case "lend":
                    if (st.lent || st.lendAsked) return "CC_PEv4_BooksLent".Translate().Resolve();
                    return SummerSchool.Books(c.map).Count == 0 ? "CC_PEv4_NoBooks".Translate().Resolve() : null;
                case "settle":
                    return st.disputeSince < 0 ? "CC_PEv4_NoDispute".Translate().Resolve() : null;
            }
            return null;
        }
    }

    /// <summary>A fellow of the society ({X}) at the gate: the society would hold its summer school on the colony's land for a week.</summary>
    public class HappeningWorker_SummerSchool : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext)
        {
            var kind = SummerSchool.Kind;
            return kind != null && CampTracker.Enabled && CampTracker.RoomFor(map) && PeoplesD.Friendly(PeoplesMath.Scholars) != null && SummerSchool.On(map) == null
                   && Colonists(map).Count > 1;
        }

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Scholars);
            if (ctx.faction == null) return false;
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Scholars_Fellow"), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            ctx.a = KinMemory.Greeter(ctx.faction, ctx.map) ?? PeoplesD.Best(ctx.map, SkillDefOf.Intellectual) ?? PeoplesD.Able(ctx.map).FirstOrDefault();
            ctx.count = SummerSchool.Books(ctx.map).Count;
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            PeopleEvents.AgeTags(tags, ctx.faction);
            if (ctx.count > 0) tags.Add("books");
        }

        public override string Unavailable(HappeningContext ctx, int option)
        {
            if (option > 1) return null;
            if (SummerSchool.On(ctx.map) != null || !CampTracker.RoomFor(ctx.map)) return "CC_PEv2_NoRoomCamp".Translate().Resolve();
            if (option == 1 && SummerSchool.Books(ctx.map).Count == 0) return "CC_PEv4_NoBooks".Translate().Resolve();
            return null;
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option <= 1 ? "welcome" : "thanks");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The school comes (SummerSchool.Start); with <see cref="lend"/>, the colony's books go to its table once it is settled.</summary>
    public class DilemmaEffect_SummerSchool : DilemmaEffect
    {
        public bool lend;

        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc == null) return;
            sheet.Add(ImpactKind.People, 1, "CC_PEv4_Impact_School".Translate(hc.faction?.Name ?? "?", PeopleEvents4Math.LessonsPerDay).Resolve(), "CC_PEv4_Impact_SchoolBrief".Translate().Resolve(), "pev4school");
            if (lend)
                sheet.Add(ImpactKind.Skill, 1, "CC_PEv4_Impact_Lend".Translate(Mathf.Min(PeopleEvents4Math.LendBooks, hc.count), Mathf.RoundToInt((PeopleEvents4Math.LessonXp(true) / PeopleEvents4Math.LessonXp(false) - 1f) * 100f)).Resolve(),
                    "CC_PEv4_Impact_LendBrief".Translate().Resolve(), "pev4lend");
        }

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            if (hc?.map == null) return;
            SummerSchool.Start(hc, lend);
            // The fellow goes back for the others.
            Arrivals.SendAway(PeoplesG.Party(hc));
        }
    }

    /// <summary>A lesson at the school's table (the play-outs' lesson, its learning by the books on the table), counted toward the best student.</summary>
    public class DilemmaEffect_SchoolLesson : DilemmaEffect_Lesson
    {
        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var c = CampTracker.About(ctx, x => x.kind?.Worker is CampWorker_SummerSchool);
            xpPerHour = PeopleEvents4Math.LessonXp(SummerSchool.StateOf(c)?.lent ?? false);
            base.Describe(ctx, sheet);
        }

        public override void Apply(DilemmaContext ctx)
        {
            var c = CampTracker.About(ctx, x => x.kind?.Worker is CampWorker_SummerSchool);
            xpPerHour = PeopleEvents4Math.LessonXp(SummerSchool.StateOf(c)?.lent ?? false);
            base.Apply(ctx);
            if (c != null && ctx?.a != null) SummerSchool.Taught(c, ctx.a);
        }
    }

    /// <summary>The colony's books lent to the school (asked from its page): carried to their table.</summary>
    public class DilemmaEffect_SchoolLend : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Apply(DilemmaContext ctx)
        {
            var c = CampTracker.About(ctx, x => x.kind?.Worker is CampWorker_SummerSchool);
            if (c != null) SummerSchool.Lend(c, ctx?.a);
        }
    }

    /// <summary>The colony's talker walks to the fellows' table to end their disputation (Rimfolk_PEv4_Settle); the verdict when they are done.</summary>
    public class DilemmaEffect_SchoolSettle : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (ctx?.a == null) return;
            int social = ctx.a.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0;
            sheet.Add(ImpactKind.People, 0, "CC_PEv4_Impact_Settle".Translate(ctx.a.LabelShort, Mathf.RoundToInt(PeopleEvents4Math.SettleOdds(social) * 100f)).Resolve(),
                "CC_PEv4_Impact_SettleBrief".Translate().Resolve(), "pev4settle");
        }

        public override void Apply(DilemmaContext ctx)
        {
            var c = CampTracker.About(ctx, x => x.kind?.Worker is CampWorker_SummerSchool);
            var table = c?.plans.FirstOrDefault(x => x.role == "table")?.Built(c.map, c.faction);
            var talker = ctx?.a;
            if (c == null || talker == null) return;
            LocalTargetInfo at = table != null ? new LocalTargetInfo(table) : new LocalTargetInfo(c.FireCell);
            if (!PlayOuts.On || !PeopleEvents4.Send(talker, SummerSchool.SettleJob, at, 1500)) SummerSchool.Settled(talker);
        }
    }
}
