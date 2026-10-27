using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A child with nobody of their own: no living parent in the colony, by blood or by adoption. The council is asked
    /// who raises them (<see cref="DilemmaWorker_Guardian"/>): a day after a child's parents die, and at other times for a
    /// lost child taken in or a ward. The answer can name an adoptive parent (<see cref="DilemmaEffect_Adopt"/>), a
    /// relation of Rimfolk's own the game shows on the Social tab like any family (Defs/Misc/Rimfolk_Relations.xml), and
    /// from then on Rimfolk treats them as parent and child: the birthday toasts, the pride when the child comes of age,
    /// the family dilemmas, and the game's own grief when one of them dies. Each child is asked about once
    /// (<see cref="CouncilGameComponent.guardianAsked"/>), and again only if they are orphaned again.
    /// </summary>
    public static class Guardians
    {
        /// <summary>Children orphaned this session, by thing id, and when: asked about a day later (lost on reload; the question still comes in its turn).</summary>
        private static readonly Dictionary<int, int> orphanedAt = new Dictionary<int, int>();

        public static void Reset() => orphanedAt.Clear();

        private static PawnRelationDef adoptive;
        private static PawnRelationDef Adoptive => adoptive ??= DefDatabase<PawnRelationDef>.GetNamedSilentFail(FamilyMath.AdoptiveParentDef);

        /// <summary>One of the colony's children (a baby too).</summary>
        public static bool Child(Pawn p) => p != null && !p.Dead && p.RaceProps.Humanlike && p.DevelopmentalStage.Juvenile() && p.IsColonist && !p.IsQuestLodger();

        /// <summary>A child of the colony with no living parent in it, by blood or by adoption.</summary>
        public static bool Unparented(Pawn p) => Child(p) && !Family.ParentsOf(p).Any(x => !x.Dead && x.IsColonist);

        /// <summary>A parent of theirs (by blood or adoption) has died: they were orphaned, rather than never had family here.</summary>
        public static bool Orphaned(Pawn p) => Family.ParentsOf(p).Any(x => x.Dead);

        /// <summary>When they were orphaned this session, or int.MaxValue: the longest waiting are asked about first.</summary>
        public static int OrphanedTick(Pawn p) => p != null && orphanedAt.TryGetValue(p.thingIDNumber, out int t) ? t : int.MaxValue;

        /// <summary>A grown-up who could raise them: one of ours, 18 or older, up to it, and not someone who dislikes the child.</summary>
        private static bool CouldRaise(Pawn b, Pawn child) =>
            b != null && b != child && !b.Dead && b.Spawned && !b.Downed && !b.InMentalState && !b.DevelopmentalStage.Juvenile() && Flavor.HumanAge(b) >= 18f
            && People.HasInnerLife(b) && !b.IsQuestLodger() && ArcUtil.Opinion(b, child) >= 0;

        /// <summary>
        /// The grown-up who would take them in: fondest of the child (and the child of them), family first, and one
        /// already raising a brother or sister of theirs before that.
        /// </summary>
        public static Pawn Best(Pawn child, IEnumerable<Pawn> adults) =>
            adults.Where(b => CouldRaise(b, child))
                .OrderByDescending(b => ArcUtil.Opinion(b, child) + ArcUtil.Opinion(child, b) / 2f + (Family.Of(b, child) ? 50f : 0f)
                                        + (Family.ChildrenOf(b).Any(c => c != child && ArcUtil.Kin(c, child)) ? 60f : 0f)).FirstOrDefault();

        /// <summary>The grown-up the child would choose: the one they like best of those who could raise them.</summary>
        public static Pawn Chosen(Pawn child, IEnumerable<Pawn> adults) =>
            adults.Where(b => CouldRaise(b, child)).OrderByDescending(b => ArcUtil.Opinion(child, b)).FirstOrDefault();

        /// <summary>
        /// <paramref name="parent"/> raises <paramref name="child"/> as their own: the relation, and the parent's partner
        /// here as well unless they dislike the child. Returns who became the child's parents.
        /// </summary>
        public static List<Pawn> Adopt(Pawn parent, Pawn child)
        {
            var parents = new List<Pawn>();
            var def = Adoptive;
            if (def == null || parent?.relations == null || child?.relations == null || parent == child) return parents;
            foreach (var x in new[] { parent }.Concat(parent.relations.DirectRelations
                         .Where(r => r.def == PawnRelationDefOf.Spouse || r.def == PawnRelationDefOf.Fiance || r.def == PawnRelationDefOf.Lover)
                         .Select(r => r.otherPawn).Where(o => CouldRaise(o, child) && o.IsColonist)).Distinct())
            {
                if (Family.ParentsOf(child).Contains(x)) continue;
                child.relations.AddDirectRelation(def, x);
                parents.Add(x);
            }
            if (parents.Count == 0) return parents;
            var comp = CouncilGameComponent.Instance;
            if (comp == null) return parents;
            string text = "CC_Guard_Chronicle".Translate(child.Named("A"), TextUtil.JoinAnd(parents.Select(x => x.LabelShort).ToList()).Named("PARENTS")).Resolve();
            comp.AddChronicle(new ChronicleEntry(ChronicleKind.Life, text, child));
            foreach (var x in parents)
                if (!comp.journals.WroteWithin(x)) comp.journals.Write(x, "Council_Journal_TookIn", null, child.Named("OTHER"));
            if (!child.DevelopmentalStage.Baby() && !comp.journals.WroteWithin(child)) comp.journals.Write(child, "Council_Journal_TakenIn", null, parent.Named("OTHER"));
            return parents;
        }

        /// <summary>Someone died: their children of the colony left with no parent here are asked about a day later.</summary>
        public static void Notify_Died(Pawn dead)
        {
            // apart: the question waits a day, and comes to the council, not to anyone's face.
            if (dead?.relations == null || CouncilGameComponent.Instance == null) return;
            var comp = CouncilGameComponent.Instance;
            int now = Find.TickManager.TicksGame;
            foreach (var c in Family.ChildrenOf(dead))
            {
                if (!Unparented(c)) continue;
                orphanedAt[c.thingIDNumber] = now;
                comp.guardianAsked.Remove(c.thingIDNumber);
            }
        }

        /// <summary>The council's hour: a child orphaned a day ago (and not yet asked about) brings the question now.</summary>
        public static void Hourly(CouncilGameComponent comp)
        {
            if (comp == null || orphanedAt.Count == 0) return;
            int now = Find.TickManager.TicksGame;
            foreach (var id in orphanedAt.Keys.ToList())
                if (now - orphanedAt[id] > GenDate.TicksPerDay * 10) orphanedAt.Remove(id);
            if (!ColonyCouncilMod.Settings.enableDilemmas || !orphanedAt.Values.Any(t => now - t >= GenDate.TicksPerDay)) return;
            OnlyOrphaned = true;
            try
            {
                comp.dilemmas.TryFire(true, d => d.Worker is DilemmaWorker_Guardian);
            }
            finally
            {
                OnlyOrphaned = false;
            }
        }

        /// <summary>While the question comes for an orphan, outside its turn: only about a child orphaned this session.</summary>
        public static bool OnlyOrphaned { get; private set; }

        /// <summary>The question about this child has been put to the council: once, until they are orphaned again.</summary>
        public static void Asked(Pawn child)
        {
            var comp = CouncilGameComponent.Instance;
            if (child == null || comp == null) return;
            orphanedAt.Remove(child.thingIDNumber);
            if (!comp.guardianAsked.Contains(child.thingIDNumber)) comp.guardianAsked.Add(child.thingIDNumber);
            if (comp.guardianAsked.Count > 40) comp.guardianAsked.RemoveRange(0, comp.guardianAsked.Count - 40);
        }
    }

    /// <summary>
    /// Who raises a child with nobody of their own (<see cref="Guardians"/>): A the child, the one orphaned longest ago first,
    /// B the grown-up who would take them in. Its own tags for the texts: "orphaned" when a parent of theirs died,
    /// "baby" for a baby.
    /// </summary>
    public class DilemmaWorker_Guardian : DilemmaWorker
    {
        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            var comp = CouncilGameComponent.Instance;
            if (comp == null || !CanHappenNow(map)) return false;
            var pawns = map.mapPawns.FreeColonistsSpawned.Where(p => !p.IsQuestLodger()).ToList();
            foreach (var a in pawns.Where(p => Guardians.Unparented(p) && !comp.guardianAsked.Contains(p.thingIDNumber) && (!Guardians.OnlyOrphaned || Guardians.OrphanedTick(p) != int.MaxValue))
                         .OrderBy(Guardians.OrphanedTick).ThenBy(_ => Rand.Value))
            {
                var b = Guardians.Best(a, pawns);
                if (b == null) continue;
                ctx = new DilemmaContext { def = def, map = map, a = a, b = b };
                Guardians.Asked(a);
                return true;
            }
            return false;
        }

        public override void AddTags(DilemmaContext ctx, HashSet<string> tags)
        {
            if (ctx?.a == null) return;
            if (Guardians.Orphaned(ctx.a)) tags.Add("orphaned");
            if (ctx.a.DevelopmentalStage.Baby()) tags.Add("baby");
        }
    }

    /// <summary>
    /// The council names who raises A (<see cref="Guardians.Adopt"/>): B, or with <see cref="childChooses"/> the grown-up
    /// A likes best, who becomes B so the rest of the answer (the evening together, the outcome) is theirs. It comes
    /// before what the answer sets going on the map (as a <see cref="DilemmaEffect_Needs"/>), so the visit is the right
    /// one's. A baby can't choose.
    /// </summary>
    public class DilemmaEffect_Adopt : DilemmaEffect_Needs
    {
        public bool childChooses;

        public override string Unavailable(DilemmaContext ctx)
        {
            if (ctx?.a == null) return null;
            if (childChooses && ctx.a.DevelopmentalStage.Baby()) return "CC_Guard_TooYoung".Translate(ctx.a.Named("A")).Resolve();
            return null;
        }

        public override void Apply(DilemmaContext ctx)
        {
            // apart: the council's word makes them family; the first evening together is the answer's visit.
            if (ctx?.a == null || ctx.a.Dead || !Guardians.Child(ctx.a)) return;
            var parent = childChooses ? Guardians.Chosen(ctx.a, ctx.Colonists) ?? ctx.b : ctx.b;
            if (parent == null || parent.Dead) return;
            ctx.b = parent;
            Guardians.Adopt(parent, ctx.a);
        }
    }
}
