using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // ===================== 18. The fellowship examined (the learned society) =====================

    /// <summary>
    /// The fellowship examined (item 18): when a colonist reaches Intellectual 12 (the summer school's best student at 10), a
    /// fellow of the society comes to examine them, that evening in a disputation where the colony gathers (Intellectual and
    /// Social count, the chance on the card), or quietly at their side (Intellectual alone, a fail seen by nobody). Passed,
    /// they are named a corresponding fellow: a lasting memory, the society's word on them (a deed naming them), a book
    /// each season by the post, and with Vanilla Skills Expanded the Researcher expertise. Failed before the colony: "Strictly
    /// speaking, that is two questions," and they are stung. The form of the colony's age: told stories, slates, books, data.
    /// </summary>
    public static class Fellowship
    {
        public const string GiftKind = "fellow";
        public const string EventDef = "Rimfolk_Evt_FellowExamined";

        public static JobDef ExamJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv4_Examine");

        private static ThoughtDef NamedThought => DefDatabase<ThoughtDef>.GetNamedSilentFail("Rimfolk_Thought_FellowNamed");

        private static ThoughtDef StungThought => DefDatabase<ThoughtDef>.GetNamedSilentFail("Rimfolk_Thought_FellowStung");

        private static int Now => Find.TickManager.TicksGame;

        public static int Intellectual(Pawn p) => p?.skills?.GetSkill(SkillDefOf.Intellectual) is SkillRecord r && !r.TotallyDisabled ? r.Level : -1;

        public static int Social(Pawn p) => p?.skills?.GetSkill(SkillDefOf.Social) is SkillRecord r && !r.TotallyDisabled ? r.Level : 0;

        public static bool BestStudent(Pawn p) => p != null && (PeopleEvents4Book.Instance?.schoolBest.Contains(p.thingIDNumber) ?? false);

        /// <summary>The colony's people the society would examine now: at the level, never named before, not examined lately.</summary>
        public static IEnumerable<Pawn> Candidates(Map map)
        {
            var b = PeopleEvents4Book.Instance;
            int now = Now;
            return PeoplesG.Grown(map).Where(p => PeopleEvents.MayGive(GiftKind, p) && (b == null || !b.ExaminedLately(p, now)) && PeopleEvents4Math.Examinable(Intellectual(p), BestStudent(p)));
        }

        /// <summary>Who they come for: the school's best student first, then the most learned.</summary>
        public static Pawn Candidate(Map map) => Candidates(map).OrderByDescending(BestStudent).ThenByDescending(Intellectual).FirstOrDefault();

        // ---------------- The verdict ----------------

        /// <summary>The examination before the colony noted (the gathering the answer set going is in play), the verdict kept for the evening's end.</summary>
        public static void Planned(HappeningContext ctx, bool pass)
        {
            var b = PeopleEvents4Book.Instance;
            if (b == null || ctx?.a == null) return;
            var play = PlayOuts.Collected().LastOrDefault(p => p.IsGathering);
            b.examPlay = play?.id ?? -1;
            b.examSince = Now;
            b.examPass = pass;
            b.examPawn = ctx.a;
            b.examiner = ctx.stranger;
            b.examFaction = ctx.faction;
            // With answers not played out on the map, it is done at once.
            if (play == null) Conclude(b, ctx.map.mapPawns.FreeColonistsSpawned.ToList());
        }

        /// <summary>A gathering is over (Interplay.Notify_GatheringOver): if it was the examination, the verdict now.</summary>
        public static void GatheringOver(PlayOut play, List<Pawn> attended, bool early)
        {
            var b = PeopleEvents4Book.Instance;
            if (b == null || play == null || b.examPlay < 0 || play.id != b.examPlay) return;
            try
            {
                Conclude(b, attended ?? new List<Pawn>());
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 4: the examination over", e);
            }
        }

        /// <summary>An examination that never came about (called off, the fellow gone): forgotten after two days.</summary>
        public static void Tidy()
        {
            var b = PeopleEvents4Book.Instance;
            if (b == null || b.examSince < 0 || Now - b.examSince < 2 * GenDate.TicksPerDay) return;
            b.examPlay = -1;
            b.examSince = -1;
            b.examPawn = b.examiner = null;
        }

        private static void Conclude(PeopleEvents4Book b, List<Pawn> heard)
        {
            var p = b.examPawn;
            var x = b.examiner;
            var f = b.examFaction;
            bool pass = b.examPass;
            b.examPlay = -1;
            b.examSince = -1;
            b.examPawn = b.examiner = null;
            Verdict(p, x, f, pass, true, heard);
        }

        /// <summary>
        /// The verdict: what the examination taught, the colonist noted as examined, and named a corresponding fellow (the
        /// memory, the society's word, the book by the post, the expertise), or failed (stung, before the colony).
        /// </summary>
        public static void Verdict(Pawn p, Pawn examiner, Faction f, bool pass, bool before, List<Pawn> heard)
        {
            if (p == null || p.Dead) return;
            // together: the verdict comes at the end of the examination itself, the evening's gathering with the fellow
            // there, or the fellow's work at the colonist's side (PeopleWorkOn brought one to the other).
            var map = p.MapHeld;
            try
            {
                p.skills?.Learn(SkillDefOf.Intellectual, PeopleEvents4Math.ExamXp, true);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 4: the examination's learning", e);
            }
            PeopleEvents4Book.Instance?.NoteExamined(p, Now);
            if (!pass)
            {
                if (examiner != null) PeoplesG.SayLine(examiner, DefDatabase<IncidentDef>.GetNamedSilentFail(EventDef), "failed");
                if (before && StungThought != null)
                {
                    using var _cause = Causes.Push("CC_PEv4_Cause_Exam".Translate(p.LabelShort).Resolve());
                    CouncilGameComponent.GiveThought(p, StungThought, examiner);
                }
                Tell.Message(NewsKind.People, Importance.Notable, map, (before ? "CC_PEv4_ExamFailed" : "CC_PEv4_ExamFailedQuiet").Translate(p.Named("PAWN"), (examiner?.LabelShort ?? "?").Named("FELLOW")).Resolve(),
                    p.Spawned ? new LookTargets(p) : LookTargets.Invalid, MessageTypeDefOf.NeutralEvent);
                return;
            }
            if (examiner != null) PeoplesG.SayLine(examiner, DefDatabase<IncidentDef>.GetNamedSilentFail(EventDef), "passed");
            PeopleEventsBook.Gave(GiftKind, p);
            if (NamedThought != null)
            {
                using var _cause = Causes.Push("CC_PEv4_Cause_Exam".Translate(p.LabelShort).Resolve());
                CouncilGameComponent.GiveThought(p, NamedThought, examiner);
            }
            bool expertise = Expertise.Give(p, "Researcher");
            if (f != null)
            {
                KinMemory.Note(f, "taught", p, "CC_PEv4_FellowName".Translate().Resolve());
                if (f.HasGoodwill && !f.IsPlayer && !f.HostileTo(Faction.OfPlayer)) WorldSituationWorker.Goodwill(f, PeopleEvents4Math.FellowGoodwill, GoodwillWhy.Helped);
                PeopleEvents4Book.Instance?.AddFellow(new FellowRecord { pawn = p, faction = f, since = Now, nextBook = Now + PeopleEvents4Math.BookEveryDays * GenDate.TicksPerDay });
            }
            Tell.Message(NewsKind.People, Importance.Notable, map,
                (expertise ? "CC_PEv4_ExamPassedExpert" : "CC_PEv4_ExamPassed").Translate(p.Named("PAWN"), (f?.Name ?? "?").Named("FACTION"), (heard?.Count ?? 0).Named("COUNT")).Resolve(),
                p.Spawned ? new LookTargets(p) : LookTargets.Invalid, MessageTypeDefOf.PositiveEvent);
            CouncilGameComponent.Instance?.AddChronicle(new ChronicleEntry(ChronicleKind.World, "CC_PEv4_FellowChronicle".Translate(p.Named("PAWN"), (f?.Name ?? "?").Named("FACTION")).Resolve(), p));
        }

        // ---------------- A book each season by the post ----------------

        /// <summary>A book of the kinds the society sends, made: the textbook likeliest, then a schematic, a novel, a recipe book.</summary>
        public static Thing MakeBook(bool textbook = false)
        {
            var defs = PeopleEvents4Math.BookDefs.Select(n => DefDatabase<ThingDef>.GetNamedSilentFail(n)).Where(d => d != null && typeof(Book).IsAssignableFrom(d.thingClass)).ToList();
            if (defs.Count == 0) return null;
            var def = textbook ? defs[0] : defs.RandomElementByWeight(d => d.defName == "TextBook" ? 3f : d.defName == "Schematic" ? 2f : 1f);
            try
            {
                return ThingMaker.MakeThing(def, def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 4: a book", e);
                return null;
            }
        }

        /// <summary>Each fellow's book when its season comes: carried to the colony's gate by one of the society, the colony told.</summary>
        public static void Books(int now)
        {
            var b = PeopleEvents4Book.Instance;
            if (b == null) return;
            Tidy();
            foreach (var r in b.fellows)
            {
                if (r?.pawn == null || r.nextBook < 0 || now < r.nextBook) continue;
                var p = r.pawn;
                var map = p.MapHeld;
                r.nextBook = now + PeopleEvents4Math.BookEveryDays * GenDate.TicksPerDay;
                if (p.Dead || p.Faction != Faction.OfPlayer || map == null || !Homes.IsHome(map) || r.faction == null || r.faction.defeated || r.faction.HostileTo(Faction.OfPlayer)) continue;
                var book = MakeBook();
                if (book == null) continue;
                r.books++;
                string text = "CC_PEv4_FellowBook".Translate(p.Named("PAWN"), r.faction.Name.Named("FACTION"), book.LabelNoCount.Named("BOOK")).Resolve();
                var spot = DropCellFinder.TradeDropSpot(map);
                if (Couriers.Deliver(map, new List<Thing> { book }, r.faction, spot))
                    Tell.Message(NewsKind.People, Importance.Flavor, map, "CC_PEv4_FellowBookComing".Translate(p.Named("PAWN"), r.faction.Name.Named("FACTION")).Resolve(), LookTargets.Invalid,
                        MessageTypeDefOf.NeutralEvent);
                else
                {
                    GenPlace.TryPlaceThing(book, spot, map, ThingPlaceMode.Near);
                    Tell.Message(NewsKind.People, Importance.Flavor, map, text, new LookTargets(new TargetInfo(spot, map)), MessageTypeDefOf.PositiveEvent);
                }
            }
        }
    }

    /// <summary>
    /// A fellow of the society ({X}) at the gate to examine {A}, who has reached the society's level in learning; tonight
    /// before the colony, or quietly at {A_objective} side.
    /// </summary>
    public class HappeningWorker_FellowExamined : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            PeoplesD.Friendly(PeoplesMath.Scholars) != null && Fellowship.Candidate(map) != null && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Scholars);
            ctx.a = Fellowship.Candidate(ctx.map);
            if (ctx.faction == null || ctx.a == null) return false;
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, Rand.Chance(0.6f) ? "Rimfolk_Scholars_Don" : "Rimfolk_Scholars_Fellow"), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            StorytellerEvents.Skilled(ctx.stranger, SkillDefOf.Intellectual, new IntRange(12, 18));
            ctx.skill = SkillDefOf.Intellectual;
            ctx.count = Fellowship.Intellectual(ctx.a);
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return true;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            PeopleEvents.AgeTags(tags, ctx.faction);
            if (Fellowship.BestStudent(ctx.a)) tags.Add("student");
        }

        /// <summary>The chance on the card: before the colony Intellectual and Social, in private Intellectual alone.</summary>
        public override float RiskChance(HappeningContext ctx, int option, float chance) =>
            option == 0 ? PeopleEvents4Math.ExamFail(Fellowship.Intellectual(ctx.a), Fellowship.Social(ctx.a), true)
            : option == 1 ? PeopleEvents4Math.ExamFail(Fellowship.Intellectual(ctx.a), 0, false)
            : base.RiskChance(ctx, option, chance);

        public override string Unavailable(HappeningContext ctx, int option)
        {
            if (option > 1) return null;
            if (ctx.a == null || ctx.a.Dead || !ctx.a.Spawned || ctx.a.Map != ctx.map) return "CC_PEv4_ExamineeGone".Translate().Resolve();
            if (option == 0 && !PlayOuts.On) return "CC_PEv4_NoGathering".Translate().Resolve();
            return null;
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option == 0 ? "tonight" : option == 1 ? "quiet" : "later");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>
    /// The examination: before the colony (the gathering set going by the answer, the verdict at its end: Fellowship.Planned),
    /// or with <see cref="quiet"/> at the colonist's side (the fellow works with them a while, PeopleWorkOn,
    /// Rimfolk_PEv4_Examine); <see cref="pass"/> is the card's roll.
    /// </summary>
    public class DilemmaEffect_FellowExamined : DilemmaEffect
    {
        public bool pass, quiet;

        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc?.a == null) return;
            if (quiet && PlayOuts.On) sheet.Play("CC_PEv4_Impact_ExamQuiet".Translate(hc.a.LabelShort, hc.stranger?.LabelShort ?? "?").Resolve(), "CC_PEv4_Impact_ExamQuietBrief".Translate().Resolve());
            if (pass)
            {
                sheet.Add(ImpactKind.Skill, 1, (Expertise.CanGive(hc.a, "Researcher") ? "CC_PEv4_Impact_FellowExpert" : "CC_PEv4_Impact_Fellow").Translate(hc.a.LabelShort).Resolve(),
                    "CC_PEv4_Impact_FellowBrief".Translate().Resolve(), "pev4fellow");
                if (hc.faction != null) sheet.Goodwill(hc.faction, PeopleEvents4Math.FellowGoodwill);
            }
            else
                sheet.Add(ImpactKind.People, -1, (quiet ? "CC_PEv4_Impact_FailQuiet" : "CC_PEv4_Impact_Fail").Translate(hc.a.LabelShort).Resolve(), "CC_PEv4_Impact_FailBrief".Translate().Resolve(),
                    "pev4fail");
        }

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            if (hc?.a == null) return;
            if (!quiet)
            {
                Fellowship.Planned(hc, pass);
                return;
            }
            var job = Fellowship.ExamJob;
            if (hc.stranger == null || job == null || !PeopleWorkOn.Begin(hc, hc.stranger, hc.a, job, LocalTargetInfo.Invalid, pass ? "pass" : "fail", 0, PeopleEvents4Math.PrivateTicks, 6f))
                Fellowship.Verdict(hc.a, hc.stranger, hc.faction, pass, false, null);
        }
    }

    /// <summary>Not now: the society notes it, and does not ask again for a while.</summary>
    public class DilemmaEffect_FellowDeclined : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Apply(DilemmaContext ctx) => PeopleEvents4Book.Instance?.NoteExamined(ctx?.a, Find.TickManager.TicksGame);
    }

    /// <summary>The quiet examination done at the colonist's side: the verdict the card's roll gave.</summary>
    public class FellowExamDone : PeopleWorkDone
    {
        public override void Done(Visit v, Pawn worker, Pawn on) =>
            Fellowship.Verdict(on, worker, worker?.Faction, PeopleWorkOn.Note(v) == "pass", false, null);

        public override void GaveUp(Visit v)
        {
            if (v?.map == null) return;
            PeopleEvents4Book.Instance?.NoteExamined(v.a, Find.TickManager.TicksGame);
            Tell.Message(NewsKind.People, Importance.Notable, v.map, "CC_PEv_WorkGivenUp".Translate((v.x?.LabelShort ?? "?").Named("WORKER"), (v.a?.LabelShort ?? "?").Named("PAWN")).Resolve(),
                LookTargets.Invalid, MessageTypeDefOf.NeutralEvent);
        }
    }
}
