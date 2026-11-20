using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A27: the year's first supper, aims said aloud (the content update, the player's go of 20 November 2026). A custom of
    /// the year's first days (Council_Dilemma_FirstSupper, Rimfolk_Dilemmas_Customs_ColonyLife.xml, kept on the calendar and
    /// toward a tradition like the other customs): at supper each says what they mean to do this year. "Everyone says it": a
    /// supper (the one feast engine), and at the table each with a life goal says it in one line; someone without one may
    /// choose one there and then from what fits them (AspirationTracker.ChooseAtSupper) and say it. "Only those who want to":
    /// those with a goal say it, nobody takes one up there. A backer at the table may answer in a line. When the supper is
    /// over, each who said theirs and those there who back them grow a little closer (the social web). The year letter reads
    /// it back (<see cref="YearLines"/>: "At the first supper Toni said she would train the dogs. She did.").
    /// </summary>
    public static class FirstSupper
    {
        /// <summary>The next at the table whose turn it is says their aim (one line); false when nobody here had one to say yet.</summary>
        public static bool SayNext(GatheringReading r, List<Pawn> here, Map map)
        {
            // together: said at the supper itself, by those standing in its area with the others (Readings.Step's own look).
            var comp = CouncilGameComponent.Instance;
            if (comp == null || !ColonyCouncilMod.Settings.aspirations) return false;
            foreach (var p in here.Where(x => !r.spoke.Contains(x) && PlayOuts.OurPerson(x) && x.IsColonist).InRandomOrder().ToList())
            {
                r.spoke.Add(p);
                var a = comp.aspirations.Active(p);
                bool fresh = false;
                if (a == null && r.chooseNew)
                {
                    a = comp.aspirations.ChooseAtSupper(comp, p);
                    fresh = a != null;
                }
                if (a?.def == null) continue;
                string line = Flavor.FromPool(fresh ? "Rimfolk_Pool_Supper_NewAim" : "Rimfolk_Pool_Supper_Aim", p);
                if (line.NullOrEmpty()) continue;
                Readings.Say(p, line.Formatted(p.Named("PAWN"), a.Goal.Named("GOAL")).Resolve());
                r.said.Add(p.ThingID);
                LifeBook.Instance?.NoteAim(new SaidAim
                {
                    pawnId = p.ThingID, name = p.LabelShort, def = a.def.defName, goal = a.Goal, year = r.year, tick = Find.TickManager.TicksGame, fresh = fresh
                });
                comp.journals.Write(p, "Rimfolk_Journal_SaidAim", null, a.Goal.Named("GOAL"));
                // A backer at the table answers, now and then.
                var backer = here.Where(o => o != p && o.relations != null && FirstSupperMath.Backs(o.relations.OpinionOf(p))).RandomElementWithFallback();
                if (backer != null && Rand.Chance(FirstSupperMath.AnswerChance))
                {
                    string answer = Flavor.FromPool("Rimfolk_Pool_Supper_Backed", backer);
                    if (!answer.NullOrEmpty()) Readings.Say(backer, answer.Formatted(backer.Named("PAWN"), p.Named("OTHER")).Resolve(), p);
                }
                return true;
            }
            return false;
        }

        /// <summary>The supper happened: each who said their aim and each there who backs them grow a little closer.</summary>
        public static void Over(GatheringReading r, PlayOut p, List<Pawn> took)
        {
            // together: they were at the supper itself (the play-out's own measure of taking part).
            var comp = CouncilGameComponent.Instance;
            if (comp == null || took == null || r.said.Count == 0) return;
            foreach (var speaker in took.Where(x => x != null && r.said.Contains(x.ThingID)).ToList())
            {
                var goal = comp.aspirations.Active(speaker)?.Goal ?? "";
                foreach (var o in took)
                {
                    if (o == null || o == speaker || o.relations == null || !FirstSupperMath.Backs(o.relations.OpinionOf(speaker))) continue;
                    comp.social.Bump(speaker, o, TieKind.Friends, FirstSupperMath.Closer, "CC_TieNote_SaidAim".Translate(speaker.Named("PAWN"), goal.Named("GOAL")).Resolve());
                }
            }
        }

        /// <summary>
        /// The year letter's paragraph on the aims said at the year's first supper within the year it tells (from
        /// <paramref name="since"/>): how each turned out, at most FirstSupperMath.MaxInLetter. Null when none was said.
        /// </summary>
        public static string YearLines(CouncilGameComponent comp, int since, int now)
        {
            var book = comp?.life;
            if (book == null) return null;
            var said = book.aims.Where(a => a.tick >= since && a.tick < now).OrderBy(a => a.tick).Take(FirstSupperMath.MaxInLetter).ToList();
            if (said.Count == 0) return null;
            var known = PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead.Where(x => x != null).ToList();
            var parts = new List<string>();
            foreach (var a in said)
            {
                var p = known.FirstOrDefault(x => x.ThingID == a.pawnId);
                if (p == null) continue;
                bool reached = comp.aspirations.all.Any(x => x.pawn == p && x.done && x.def?.defName == a.def && x.doneTick >= a.tick);
                bool stillAt = comp.aspirations.Active(p)?.def?.defName == a.def;
                var outcome = FirstSupperMath.Outcome(reached, p.Dead, stillAt);
                parts.Add(("CC_Year_SupperAim_" + outcome).Translate(p.Named("PAWN"), (a.goal ?? "").Named("GOAL")).Resolve());
            }
            if (parts.Count == 0) return null;
            return "CC_Year_SupperAims".Translate().Resolve() + " " + string.Join(" ", parts);
        }
    }

    /// <summary>
    /// The year's first supper (A27): a supper (<see cref="DilemmaEffect_Feast"/>), and at the table each with a life goal says
    /// it, one line each (<see cref="FirstSupper"/>); with <see cref="chooseNew"/>, someone without one may choose one there.
    /// </summary>
    public class DilemmaEffect_FirstSupper : DilemmaEffect_Feast
    {
        /// <summary>Someone with no life goal may choose one at the table ("everyone says it").</summary>
        public bool chooseNew;

        protected override string Extra(DilemmaContext ctx) =>
            (chooseNew ? "CC_Life_Supper_ImpactAll" : "CC_Life_Supper_ImpactSome").Translate().Resolve();

        protected override PlayOut Create(DilemmaContext ctx)
        {
            var p = base.Create(ctx);
            if (p == null) return null;
            var r = Readings.Add(p, ReadingKind.Aims);
            if (r != null)
            {
                r.chooseNew = chooseNew;
                r.year = ctx?.map != null ? GenLocalDate.Year(ctx.map) : 0;
            }
            return p;
        }
    }
}
