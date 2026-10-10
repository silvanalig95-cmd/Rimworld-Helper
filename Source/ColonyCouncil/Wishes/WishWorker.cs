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

        /// <summary>
        /// Out of patience, the colonist does something about it themselves: plans the lamp, switches their
        /// own work back on, digs the grave. Returns a line for the message ({PAWN}, {THING}...), or null if
        /// there's nothing they can do on their own.
        /// </summary>
        public virtual string TrySelfHelp(Wish wish) => null;

        /// <summary>Plans <paramref name="thing"/> in the colonist's own room. Shared by the furniture-like wishes.</summary>
        protected static bool PlanInOwnRoom(Pawn p, ThingDef thing) => PlanInRoom(RoomUtil.PrivateRoom(p), p, thing);

        protected static bool PlanInRoom(Room room, Pawn p, ThingDef thing)
        {
            var map = p?.MapHeld;
            if (room == null || map == null || thing == null || !Avail.CanBuild(thing, map)) return false;
            var stuff = thing.MadeFromStuff ? Avail.BestStuff(thing, map) : null;
            if (!PersonalTouchTracker.FindCell(room, map, thing, stuff, out var cell, out var rot)) return false;
            GenConstruct.PlaceBlueprintForBuild(thing, cell, map, rot, Faction.OfPlayer, stuff);
            return true;
        }

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
