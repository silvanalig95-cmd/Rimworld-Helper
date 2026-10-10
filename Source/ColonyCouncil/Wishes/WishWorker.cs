using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Decides when a wish makes sense for a colonist and when it has come true.
    /// Workers must only propose things the colony can actually do (see <see cref="Avail"/>).
    /// </summary>
    public abstract class WishWorker
    {
        public WishDef def;

        /// <summary>Candidate wishes where <paramref name="pawn"/> is the one who benefits.</summary>
        public abstract IEnumerable<WishCandidate> CandidatesFor(Pawn pawn);

        public abstract bool IsFulfilled(Wish wish);

        /// <summary>False when the wish no longer makes sense (a partner left, the friend's body is gone).</summary>
        public virtual bool StillValid(Wish wish) => true;

        /// <summary>Human-readable progress, e.g. "Impressiveness 32 / 50".</summary>
        public virtual string ProgressText(Wish wish) => null;

        /// <summary>0..1, or a negative number when progress isn't measurable.</summary>
        public virtual float Progress(Wish wish) => -1f;

        public virtual RoomTemplateDef SuggestedTemplate(Wish wish) => def.template;

        public virtual LookTargets LookAt(Wish wish) => new LookTargets(wish.Beneficiary);

        public virtual InspirationDef InspirationFor(Wish wish) => null;

        public virtual string FulfilledWhen(Wish wish) => wish.Format(def.fulfilledWhen);

        public virtual string RimTalkTopic(Wish wish) => wish.Format(def.rimTalkTopic ?? def.label);

        // ---------- Helpers for workers ----------

        protected static Map HomeMap(Pawn p) => p?.MapHeld;

        protected static bool HasMemory(Pawn p, ThoughtDef thought)
        {
            if (thought == null) return false;
            var mem = p?.needs?.mood?.thoughts?.memories;
            return mem != null && mem.GetFirstMemoryOfDef(thought) != null;
        }

        protected static bool HasMemory(Pawn p, string thoughtDefName)
        {
            return HasMemory(p, DefDatabase<ThoughtDef>.GetNamedSilentFail(thoughtDefName));
        }

        protected static bool HasTrait(Pawn p, TraitDef t) => t != null && p?.story?.traits != null && p.story.traits.HasTrait(t);

        protected static int TraitDegree(Pawn p, TraitDef t) =>
            t != null && p?.story?.traits != null && p.story.traits.HasTrait(t) ? p.story.traits.DegreeOfTrait(t) : 0;

        protected static WishCandidate Candidate(float weight, string reason) => new WishCandidate { weight = weight, reason = reason };
    }
}
