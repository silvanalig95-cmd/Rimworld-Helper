using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Put on one of Rimfolk's ThoughtDefs that is its own telling of a mood the game already has (teased too far is the
    /// game's "slighted", no coat in the rain its "soaking wet"): once the defs are loaded, the thought takes the game
    /// mood's nullifiers (traits, trait degrees, genes, hediffs, precepts), whichever mod added them. So whoever the game
    /// spares the original (a drakonori's inner peace, a nereid's scales, a table-resistant stomach) is spared Rimfolk's
    /// telling of it too.
    /// </summary>
    public class LikeThought : DefModExtension
    {
        /// <summary>The game's mood this one tells again.</summary>
        public ThoughtDef thought;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            if (thought == null) yield return "likeThought needs the game's thought";
        }

        /// <summary>Once at startup: every thought with this takes its original's nullifiers.</summary>
        public static void Apply()
        {
            foreach (var def in DefDatabase<ThoughtDef>.AllDefsListForReading)
            {
                var like = def.GetModExtension<LikeThought>()?.thought;
                if (like == null || like == def) continue;
                Merge(ref def.nullifyingTraits, like.nullifyingTraits);
                Merge(ref def.nullifyingTraitDegrees, like.nullifyingTraitDegrees);
                Merge(ref def.nullifyingGenes, like.nullifyingGenes);
                Merge(ref def.nullifyingHediffs, like.nullifyingHediffs);
                Merge(ref def.nullifyingPrecepts, like.nullifyingPrecepts);
            }
        }

        private static void Merge<T>(ref List<T> into, List<T> from)
        {
            if (from.NullOrEmpty()) return;
            if (into == null) into = new List<T>();
            foreach (var x in from)
                if (x != null && !into.Contains(x)) into.Add(x);
        }
    }

    /// <summary>
    /// Put on one of Rimfolk's ThoughtDefs that comes from something heard (music in the air, birdsong, a traveller's
    /// songs): nobody who can't hear (the game's Hearing: a deaf gene, ears lost in a raid) is given it.
    /// </summary>
    public class Heard : DefModExtension
    {
        /// <summary>They may have this thought: it isn't heard, or they can hear.</summary>
        public static bool CanHave(Pawn p, ThoughtDef def) => def == null || !def.HasModExtension<Heard>() || BodyFit.Hears(p);
    }
}
