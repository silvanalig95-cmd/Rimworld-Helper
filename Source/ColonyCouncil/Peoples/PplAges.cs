using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    // The peoples' older events in their own age (docs/content-round-7-peoples-ages.md): the 96 events of rounds 1 to 3 and
    // releases C to G were told in the colony's age; now each is told in the visiting people's age and ways as rounds 4 to
    // 10 are (their texts tagged [age0] to [age3], each worker's AddTags with PeopleEvents.AgeTags), and what they bring, ask
    // or pay in goods is of their age (PplAgesMath). The colony's own answer stays in the colony's own means.

    /// <summary>
    /// A caller told from data (HappeningWorker_Setting) who is one of the peoples' own: the order's hospitaller on the round,
    /// the league's outrider and the society's night of the fire. Everything as the setting does it, and the people's age for
    /// the words.
    /// </summary>
    public class HappeningWorker_PplAgesSetting : HappeningWorker_Setting
    {
        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            base.AddTags(ctx, tags);
            if (ctx?.faction != null) PeopleEvents.AgeTags(tags, ctx.faction);
        }
    }

    /// <summary>What the peoples' older events bring, ask or pay, by the people's age (PplAgesMath has the rules).</summary>
    public static class PplAges
    {
        /// <summary>The people's age (0 tribal to 3 spacer).</summary>
        public static int Of(Faction f) => PeopleEvents.AgeOf(f);

        /// <summary>A Signal Line of the old ages raises its flag tower where a later one raises a relay mast.</summary>
        public static ThingDef TowerDef => DefDatabase<ThingDef>.GetNamedSilentFail("Rimfolk_PplR8_LineTower");

        /// <summary>What the Line raises for the colony: a flag tower from a Line of the old ages (when the game has it), else the relay mast.</summary>
        public static ThingDef MastFor(Faction line) => PplAgesMath.Tower(Of(line)) ? TowerDef ?? PeoplesF.MastDef : PeoplesF.MastDef;

        /// <summary>The colony's mast or tower: either is the colony's line to the Signal Line.</summary>
        public static bool IsMast(ThingDef def) => def != null && (def == PeoplesF.MastDef || def == TowerDef);
    }
}
