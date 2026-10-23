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

        /// <summary>
        /// The colonist's beliefs rule this out (Wishes/Beliefs.cs): the def's <see cref="WishDef.forbiddenBy"/>, or what
        /// it asks for. <paramref name="pawn"/> is who it's for; <paramref name="other"/> the other one it's about. A
        /// candidate that clashes is never raised, and an open wish that comes to clash is let go quietly.
        /// </summary>
        public virtual bool AgainstBeliefs(Pawn pawn, Pawn other, ThingDef thing) => Beliefs.AgainstWish(def, pawn, thing);

        public bool AgainstBeliefs(Wish wish) => wish != null && AgainstBeliefs(wish.Beneficiary, wish.other, wish.thingDef);

        /// <summary>
        /// False when what the wish asks for can't be had at all any more (its mod was removed, nothing like it can
        /// be built, made or bought). A wish like that lapses without blame instead of expiring with a penalty.
        /// </summary>
        public virtual bool StillObtainable(Wish wish) => !wish.thingLost;

        /// <summary>Human-readable progress, e.g. "Impressiveness 32 / 50".</summary>
        public virtual string ProgressText(Wish wish) => null;

        /// <summary>0..1, or a negative number when progress isn't measurable.</summary>
        public virtual float Progress(Wish wish) => -1f;

        public virtual RoomTemplateDef SuggestedTemplate(Wish wish) => def.template;

        /// <summary>Called while the colonist is away from home (in a caravan): wishes about being away can note it.</summary>
        public virtual void WhileAway(Wish wish) { }

        public virtual LookTargets LookAt(Wish wish) => new LookTargets(wish.Beneficiary);

        public virtual InspirationDef InspirationFor(Wish wish) => null;

        public virtual string FulfilledWhen(Wish wish) => wish.Format(def.fulfilledWhen);

        /// <summary>
        /// Out of patience, the colonist does something about it themselves: plans the lamp, switches their
        /// own work back on, digs the grave. Returns a line for the message ({PAWN}, {THING}...), or null if
        /// there's nothing they can do on their own.
        /// </summary>
        public virtual string TrySelfHelp(Wish wish) => null;

        /// <summary>
        /// The player promised it: some wishes can be set in motion right away (call the party, mark the prey).
        /// Returns a message key or text ({PAWN}, {SUBJECT}...), or null if a promise is all it gets.
        /// </summary>
        public virtual string OnPromised(Wish wish) => null;

        // ---------- Promises kept: a granted lasting wish is watched for a while (Wishes/Kept.cs) ----------

        /// <summary>Whether this wish, once granted, is watched: something that stays and can be taken away. By default the def's lasting flag.</summary>
        public virtual bool Lasting(Wish wish) => def.lasting;

        /// <summary>
        /// For a granted lasting wish: is what was given still there? By default the wish's own fulfilment check. A worker
        /// says Unclear when it can't tell right now (their room is gone, and the room's own watch covers that) and Over
        /// when it no longer makes sense to watch (the partner left, the pet died, the child grew up). Things being moved,
        /// reinstalled or rebuilt are allowed for by the watch itself.
        /// </summary>
        public virtual KeptCheck Holds(Wish wish) => IsFulfilled(wish) ? KeptCheck.Holds : KeptCheck.Gone;

        /// <summary>It belongs in their room: what's kept, and plans to build it again, are looked for there.</summary>
        public virtual bool InTheirRoom => false;

        /// <summary>The room it belongs in: their own bedroom.</summary>
        public virtual Room TheirRoom(Wish wish) => RoomUtil.PrivateRoom(wish.Beneficiary);

        /// <summary>
        /// Whether a building (or a floor) of this kind is what the wish is about: a plan or frame of one being built where
        /// it belongs counts as still there for a while.
        /// </summary>
        public virtual bool Concerns(Wish wish, BuildableDef d) => d != null && d == wish.thingDef && !(d is ThingDef td && td.IsFrame);

        /// <summary>
        /// The things that make it hold right now (a bed, a telescope), remembered while it holds, so that one being moved
        /// or stored isn't taking it away, and one lost to a fire or a raid isn't blamed on the council.
        /// </summary>
        public virtual IEnumerable<Thing> KeptThings(Wish wish)
        {
            var map = wish.Beneficiary?.MapHeld;
            if (map == null) yield break;
            if (InTheirRoom)
            {
                var room = TheirRoom(wish);
                if (room == null || room.PsychologicallyOutdoors) yield break;
                var things = room.ContainedAndAdjacentThings;
                for (int i = 0; i < things.Count; i++)
                {
                    var t = things[i];
                    if (t.def.category == ThingCategory.Building && room.ContainsCell(t.Position) && Concerns(wish, t.def)) yield return t;
                }
                yield break;
            }
            var list = map.listerBuildings.allBuildingsColonist;
            for (int i = 0; i < list.Count; i++)
                if (Concerns(wish, list[i].def)) yield return list[i];
        }

        /// <summary>Just granted, before the watch starts: anything to remember (which animal is the new pet).</summary>
        public virtual void OnGranted(Wish wish)
        {
        }

        /// <summary>What kind of thing was given, for the journal's lines: room, bed, thing, stock, work or pet.</summary>
        public virtual string KeptKind(Wish wish) => InTheirRoom ? "room" : "thing";

        /// <summary>What's gone, for the message: the def's own line, else a general one.</summary>
        public virtual string LostText(Wish wish) => !def.lostText.NullOrEmpty()
            ? wish.Format(def.lostText)
            : "CC_Kept_Lost_Generic".Translate(wish.Beneficiary.Named("PAWN"), wish.Label.Named("WISH")).Resolve();

        /// <summary>
        /// The animal a pet wish brought (Wishes/Kept.cs): one bonded to them, else one they're master of, else the colony's
        /// newest. Null if the colony has none here.
        /// </summary>
        protected static Pawn NewAnimalFor(Pawn person)
        {
            var map = person?.MapHeld;
            if (map == null) return null;
            Pawn best = null;
            int bestRank = -1;
            foreach (var a in map.mapPawns.SpawnedColonyAnimals)
            {
                if (a == null || a.Dead) continue;
                int rank = a.relations != null && a.relations.DirectRelationExists(PawnRelationDefOf.Bond, person) ? 2 : a.playerSettings?.Master == person ? 1 : 0;
                if (best == null || rank > bestRank || (rank == bestRank && a.thingIDNumber > best.thingIDNumber))
                {
                    best = a;
                    bestRank = rank;
                }
            }
            return best;
        }

        /// <summary>A pet given: still the colony's. Dead isn't taken back; sold, released or run off is.</summary>
        protected static KeptCheck PetKept(Pawn pet)
        {
            if (pet == null || pet.Dead || pet.Discarded) return KeptCheck.Over;
            return pet.Faction == Faction.OfPlayer ? KeptCheck.Holds : KeptCheck.Gone;
        }

        /// <summary>
        /// Plans <paramref name="thing"/> in the colonist's own room. Shared by the furniture-like wishes. With
        /// <paramref name="ownAccord"/> (a colonist helping themselves) only plain materials: wood, stone blocks or bone,
        /// or nothing is planned. Promised by the council, plain materials when it can be made of them.
        /// </summary>
        protected static bool PlanInOwnRoom(Pawn p, ThingDef thing, bool ownAccord = false) => PlanInRoom(RoomUtil.PrivateRoom(p), p, thing, ownAccord);

        protected static bool PlanInRoom(Room room, Pawn p, ThingDef thing, bool ownAccord = false)
        {
            var map = p?.MapHeld;
            if (room == null || map == null || thing == null || !Avail.CanBuild(thing, map)) return false;
            if (ownAccord && !Avail.PlainWithinReach(thing, map, out _)) return false;
            // Plain materials (wood, stone blocks, bone) when it can be made of them; the colony's steel only when nothing plainer will do.
            var stuff = thing.MadeFromStuff ? Avail.PlainStuff(thing, map) ?? (ownAccord ? null : Avail.BestStuff(thing, map)) : null;
            if (ownAccord && thing.MadeFromStuff && stuff == null) return false;
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
