using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A toy for the little one (A11 of docs/content-update-proposals.md, the player's go of 20 November 2026). A grown-up
    /// fond of a child of three to twelve at home (a parent, an elder or a friend of the child's, all alike: nothing here
    /// follows family lines, ToyMath.MayAsk) asks for a toy made for them at the bench: with Vanilla Christmas Expanded one
    /// of its toys (wooden, plushie or metal, from its own recipes), without it a small sculpture. Met when one is made
    /// after the wish: the one who asked takes it to the child by hand (LifeHands, the play-out engine's handing over), and
    /// the child has the mod's own toy memory when it has one that is a memory, else Rimfolk's. Only what the colony can
    /// make: its recipe researched and its bench built or buildable. No harm.
    /// </summary>
    public class WishWorker_Toy : WishWorker
    {
        private static readonly string[] ToyNames = { "VCE_Toy_Wooden", "VCE_Toy_Cloth", "VCE_Toy_Metal" };

        /// <summary>Vanilla Christmas Expanded's toys this game has.</summary>
        public static List<ThingDef> Toys => ToyNames.Select(n => DefDatabase<ThingDef>.GetNamedSilentFail(n)).Where(d => d != null).ToList();

        private static ThingDef Carving => DefDatabase<ThingDef>.GetNamedSilentFail("SculptureSmall");

        /// <summary>What can be made for a child here: the mod's toys first, else a small sculpture; empty when neither.</summary>
        public static List<ThingDef> Makeable(Map map, out bool toys)
        {
            toys = false;
            if (map == null) return new List<ThingDef>();
            var list = Toys;
            if (list.Count > 0 && WishWorker_Fix.CanMake(map, list))
            {
                toys = true;
                return list;
            }
            var carving = Carving;
            return carving != null && WishWorker_Fix.CanMake(map, new List<ThingDef> { carving }) ? new List<ThingDef> { carving } : new List<ThingDef>();
        }

        private static List<ThingDef> Wanted(Wish w) => w.subjectKey == "toy" ? Toys : Carving != null ? new List<ThingDef> { Carving } : new List<ThingDef>();

        /// <summary>The children it could be asked for, fondest first.</summary>
        public static IEnumerable<Pawn> ChildrenFor(Pawn asker)
        {
            var map = asker?.MapHeld;
            var book = LifeVarietyBook.Instance;
            if (map == null || book == null || asker.DevelopmentalStage.Juvenile() || asker.relations == null) yield break;
            float askerAge = Flavor.HumanAge(asker);
            foreach (var c in map.mapPawns.FreeColonistsSpawned.Where(c => c != asker && c.DevelopmentalStage.Child() && ToyMath.ChildAge(Flavor.HumanAge(c)))
                         .OrderByDescending(c => asker.relations.OpinionOf(c)))
            {
                if (book.ToyLately(c)) continue;
                bool parent = asker.GetRelations(c).Any(r => r == PawnRelationDefOf.Child || FamilyMath.AdoptedChild(r.defName));
                if (!ToyMath.MayAsk(parent, askerAge, asker.relations.OpinionOf(c))) continue;
                // One toy asked for a child at a time.
                if (CouncilGameComponent.Instance?.wishes.Any(w => w.state == WishState.Open && w.def?.Worker is WishWorker_Toy && w.other == c) ?? false) continue;
                yield return c;
            }
        }

        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            var map = HomeMap(pawn);
            if (map == null || !ModsConfig.BiotechActive) yield break;
            var child = ChildrenFor(pawn).FirstOrDefault();
            if (child == null) yield break;
            var defs = Makeable(map, out bool toys);
            if (defs.Count == 0) yield break;
            var c = Candidate(ToyMath.Weight(pawn.relations.OpinionOf(child)), "CC_Reason_Toy".Translate(pawn.Named("PAWN"), child.Named("OTHER")).Resolve());
            c.other = child;
            c.subjectKey = toys ? "toy" : "carving";
            c.subjectLabel = (toys ? "CC_Life2_Toy" : "CC_Life2_Carving").Translate().Resolve();
            c.thingDef = defs[0];
            c.baseline = Count(map, defs);
            yield return c;
        }

        private static int Count(Map map, List<ThingDef> defs)
        {
            if (map == null || defs.Count == 0) return 0;
            int n = defs.Sum(d => map.listerThings.ThingsOfDef(d).Count(t => t.Spawned));
            n += map.listerThings.ThingsOfDef(ThingDefOf.MinifiedThing).Count(t => t is MinifiedThing m && defs.Contains(m.InnerThing?.def));
            return n;
        }

        public override bool IsFulfilled(Wish wish) => Count(wish.Beneficiary.MapHeld, Wanted(wish)) > wish.baseline;

        public override bool StillValid(Wish wish)
        {
            var c = wish.other;
            return c != null && !c.Dead && c.DevelopmentalStage.Child() && c.MapHeld == wish.Beneficiary?.MapHeld;
        }

        public override bool StillObtainable(Wish wish) => base.StillObtainable(wish) && Wanted(wish).Count > 0 && WishWorker_Fix.CanMake(wish.Beneficiary.MapHeld, Wanted(wish));

        public override LookTargets LookAt(Wish wish) => new LookTargets(new[] { wish.pawn, wish.other }.Where(p => p != null && p.Spawned));

        /// <summary>Met: the one who asked takes the newest of what was made to the child, by hand.</summary>
        public override void OnMet(Wish wish)
        {
            var child = wish.other;
            var asker = wish.pawn;
            var map = asker?.MapHeld;
            if (child == null || asker == null || map == null) return;
            LifeVarietyBook.Instance?.NoteToy(child);
            var made = LifeHands.Newest(map, Wanted(wish));
            var childMood = ChildMood();
            var askerMood = DefDatabase<ThoughtDef>.GetNamedSilentFail("Rimfolk_Thought_ToyHanded");
            string label = "CC_Life2_ToyPlay".Translate(child.Named("OTHER")).Resolve();
            if (made != null && LifeHands.Give(asker, child, made, label, new List<HeldMood> { LifeHands.Mood(child, childMood, asker), LifeHands.Mood(asker, askerMood, child) }) != null)
                return;
            // apart: nothing carried (play-outs off, or the toy gone): the moods now, as a met wish's always came, and the
            // message says only that it is made, not that it was handed over.
            if (childMood != null) CouncilGameComponent.GiveThought(child, childMood, asker);
            if (askerMood != null) CouncilGameComponent.GiveThought(asker, askerMood, child);
            if (made != null)
                Tell.Minor(NewsKind.People, Importance.Flavor, map, "CC_Life2_ToyMade".Translate(asker.Named("PAWN"), child.Named("OTHER"), PlayGive.Label(made).Named("THING")).Resolve(),
                    made, MessageTypeDefOf.PositiveEvent);
        }

        /// <summary>The child's memory: Vanilla Christmas Expanded's own for a toy, when it is a memory; else Rimfolk's.</summary>
        private static ThoughtDef ChildMood()
        {
            var mod = DefDatabase<ThoughtDef>.GetNamedSilentFail("VCE_Thought_Toy_Children");
            if (mod != null && mod.IsMemory) return mod;
            return DefDatabase<ThoughtDef>.GetNamedSilentFail("Rimfolk_Thought_ToyGiven");
        }
    }
}
