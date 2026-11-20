using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The elders' day, an autumn custom (A28 of docs/content-update-proposals.md, the player's go of 20 November 2026;
    /// Rimfolk_Dilemma_EldersDay in Defs/DilemmaDefs/Rimfolk_Dilemmas_Customs_LifeVariety.xml). With two colonists of sixty or
    /// more at home, or the elder's seat held, someone young asks to honour them. "The whole day": in the day the colony's
    /// children each make something small at the bench for one of the elders and carry it over (the answers' own making and
    /// handing over, DilemmaEffect_Made); in the evening a supper raised to the elders, at which each elder there tells one
    /// thing from their life, one line each (the gathering's readings, ColonyLife/Readings.cs), and anyone at the table may
    /// answer. "Supper only": the supper and the tales. "Not this year". Kept three years running it is a tradition. The
    /// elders who told theirs are glad of the evening. No harm.
    /// </summary>
    public static class EldersDay
    {
        public const string TalePool = "Rimfolk_Pool_EldersDay_Tale", AnswerPool = "Rimfolk_Pool_EldersDay_Answer";

        public static bool IsElder(Pawn p) => p != null && p.RaceProps.Humanlike && EldersMath.IsElder(Flavor.HumanAge(p), Interplay.Holds(p, "Council_Office_Elder"));

        /// <summary>The colony's elders at this home.</summary>
        public static List<Pawn> Elders(Map map) =>
            map?.mapPawns.FreeColonistsSpawned.Where(p => PlayOuts.OurPerson(p) && !p.DevelopmentalStage.Juvenile() && IsElder(p)).ToList() ?? new List<Pawn>();

        public static bool Possible(Map map)
        {
            if (map == null) return false;
            var elders = Elders(map);
            bool seat = elders.Any(p => Interplay.Holds(p, "Council_Office_Elder"));
            return EldersMath.MayAsk(elders.Count, seat);
        }

        /// <summary>The next elder at the table whose turn it is tells one thing from their life; false when none here had one yet.</summary>
        public static bool TellNext(GatheringReading r, List<Pawn> here)
        {
            // together: said at the supper itself, by those standing in its area with the others (Readings.Step's own look).
            foreach (var p in here.Where(x => r.said.Contains(x.ThingID) && !r.spoke.Contains(x)).ToList())
            {
                r.spoke.Add(p);
                string line = Flavor.FromPool(TalePool, p);
                if (line.NullOrEmpty()) continue;
                Readings.Say(p, line.Formatted(p.Named("PAWN")).Resolve());
                var listener = here.Where(o => o != p && !r.said.Contains(o.ThingID)).RandomElementWithFallback();
                if (listener != null && Rand.Chance(0.5f))
                {
                    string answer = Flavor.FromPool(AnswerPool, listener);
                    if (!answer.NullOrEmpty()) Readings.Say(listener, answer.Formatted(listener.Named("PAWN"), p.Named("OTHER")).Resolve(), p);
                }
                return true;
            }
            return false;
        }

        /// <summary>The supper happened: the elders who told theirs are glad of it, and the colony's people there a little closer to them.</summary>
        public static void Over(GatheringReading r, PlayOut p, List<Pawn> took)
        {
            // together: they were at the supper itself (the play-out's own measure of taking part).
            var comp = CouncilGameComponent.Instance;
            if (comp == null || took == null) return;
            var mood = DefDatabase<ThoughtDef>.GetNamedSilentFail("Rimfolk_Thought_EldersHonoured");
            foreach (var elder in r.spoke.Where(x => x != null && took.Contains(x)))
            {
                if (mood != null) CouncilGameComponent.GiveThought(elder, mood);
                comp.journals.WriteText(elder, "CC_Life2_EldersJournal".Translate(elder.Named("PAWN")).Resolve());
                foreach (var o in took.Where(o => o != elder && !r.said.Contains(o.ThingID)).Take(6))
                    comp.social?.Bump(o, elder, TieKind.Friends, 1.5f, "CC_Life2_EldersNote".Translate(elder.Named("PAWN")).Resolve());
            }
        }

        /// <summary>
        /// "The whole day": each of the colony's children (four at most) makes something small for one of the elders, in turn, and
        /// carries it over (the answers' own making and handing over).
        /// </summary>
        public static int ChildrenMake(DilemmaContext ctx)
        {
            var map = ctx?.map;
            var elders = Elders(map);
            if (map == null || elders.Count == 0) return 0;
            var children = map.mapPawns.FreeColonistsSpawned.Where(p => p.DevelopmentalStage.Child() && PlayOuts.OurPerson(p)).Take(EldersMath.MostGifts).ToList();
            int n = 0;
            for (int k = 0; k < children.Count; k++)
            {
                var elder = elders[EldersMath.ElderFor(k, elders.Count)];
                var sub = new DilemmaContext { def = ctx.def, map = map, a = children[k], b = elder };
                var made = new DilemmaEffect_Made
                {
                    things = "SculptureSmall", maker = PlayWho.A, forWhom = PlayWho.B, giver = PlayWho.A, skill = SkillDefOf.Artistic, quality = QualityCategory.Poor,
                    hours = new IntRange(8, 16), makeHours = 1f, label = "CC_Life2_EldersMadeLabel".Translate(children[k].Named("A"), elder.Named("B")).Resolve()
                };
                LifeVarietyBook.Safe("a child's piece for an elder", () => made.Apply(sub));
                n++;
            }
            return n;
        }
    }

    /// <summary>
    /// The elders' supper (A28): a supper raised to the colony's elders, at which each elder tells one thing from their life
    /// (<see cref="EldersDay"/>); with <see cref="children"/>, the children's pieces made for them by day too.
    /// </summary>
    public class DilemmaEffect_EldersDay : DilemmaEffect_Feast
    {
        /// <summary>"The whole day": the children make something for the elders by day.</summary>
        public bool children;

        public DilemmaEffect_EldersDay()
        {
            toast = true;
        }

        protected override string Extra(DilemmaContext ctx)
        {
            int elders = EldersDay.Elders(ctx?.map).Count;
            string s = "CC_Life2_Elders_Impact".Translate(elders.Named("COUNT")).Resolve();
            if (children && ctx?.map != null && ctx.map.mapPawns.FreeColonistsSpawned.Any(p => p.DevelopmentalStage.Child())) s += "CC_Life2_Elders_ImpactChildren".Translate().Resolve();
            return s;
        }

        protected override PlayOut Create(DilemmaContext ctx)
        {
            toastLine = "CC_Life2_EldersToast".Translate().Resolve();
            var p = base.Create(ctx);
            if (p == null) return null;
            var r = Readings.Add(p, ReadingKind.Elders);
            if (r != null)
                foreach (var e in EldersDay.Elders(ctx.map))
                    r.said.Add(e.ThingID);
            var book = LifeVarietyBook.Instance;
            if (book != null && ctx.map != null) book.eldersYear = GenLocalDate.Year(ctx.map);
            if (children) EldersDay.ChildrenMake(ctx);
            return p;
        }
    }

    /// <summary>The elders' day's question: autumn, with two elders at home or the elder's seat held, once a year.</summary>
    public class DilemmaWorker_EldersDay : DilemmaWorker_Custom
    {
        public override bool CanHappenNow(Map map) =>
            base.CanHappenNow(map) && EldersDay.Possible(map) && LifeVarietyBook.Instance?.eldersYear != GenLocalDate.Year(map);

        protected override bool Extra(DilemmaContext ctx)
        {
            if (!base.Extra(ctx)) return false;
            // Someone young asks, never one of the elders themselves.
            return ctx.a != null && !EldersDay.IsElder(ctx.a) && EldersMath.Young(Flavor.HumanAge(ctx.a));
        }
    }
}
