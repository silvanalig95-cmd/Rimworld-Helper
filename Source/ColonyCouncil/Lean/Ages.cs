using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The form a feature takes between the colony and a people: the word from the colony's own age (its means: what it
    /// calls the thing and does it with), the rite from the other people's age (their ways: how it is done with them).
    /// </summary>
    public sealed class AgeForm
    {
        public AgeFeature feature;
        /// <summary>The colony's age, which the word comes from.</summary>
        public WorldAge ours;
        /// <summary>The other people's age, which the rite comes from.</summary>
        public WorldAge theirs;
        /// <summary>The other people are the Empire: it keeps its own titles and honor, which the feature speaks of beside the rite (WorldMechanics.Title).</summary>
        public bool empire;
        /// <summary>The colony's word for it, by its own age: "an IOU", "a debt of honor".</summary>
        public string word;
        /// <summary>How it is done with them, by their age: "a gift owed back", "oaths sworn before witnesses".</summary>
        public string rite;
    }

    /// <summary>
    /// The ages, read in one place (DESIGN.md, "Ages"): tribal, medieval, industrial or spacer, from a tech level as
    /// <see cref="LedgerMath.AgeOf"/> reads it, with the Empire marked apart. The colony's age sets its means and a
    /// people's age its ways; <see cref="Form"/> gives each feature's word and rite from the keyed strings
    /// (CC_Form_Feature_Age, Rimfolk_Ages.xml), by the rule in <see cref="AgesMath"/>. Flavor's era tags and the world
    /// ledger's people view ask here.
    /// </summary>
    public static class Ages
    {
        /// <summary>
        /// The colony's age: its own tech level or what it has researched, whichever is higher (Avail.EffectiveTech). The
        /// player's faction has one, so today every home has the same.
        /// </summary>
        public static WorldAge Ours => LedgerMath.AgeOf((int)Avail.EffectiveTech);

        /// <summary>A colony's age (its means). Every home has the colony's one age until the homes keep their own.</summary>
        public static WorldAge Of(Map colony) => Ours;

        /// <summary>A people's age (its ways), by its tech level; the player's own and none at all are the colony's.</summary>
        public static WorldAge Of(Faction people)
        {
            if (people == null || people.IsPlayer) return Ours;
            return LedgerMath.AgeOf((int)(people.def?.techLevel ?? TechLevel.Industrial));
        }

        /// <summary>A people of royal titles (the Empire): its own titles and honor, apart from the ages.</summary>
        public static bool IsEmpire(Faction people) => people?.def != null && people.def.HasRoyalTitles;

        /// <summary>An age as lines are tagged: tribal, medieval, industrial, spacer.</summary>
        public static string Tag(WorldAge age) => AgesMath.Tag(age);

        /// <summary>
        /// A feature's form between two ages: its word from <paramref name="ours"/> (the colony's means), its rite from
        /// <paramref name="theirs"/> (the other people's ways); <paramref name="empire"/> marks the other people as the
        /// Empire.
        /// </summary>
        public static AgeForm Form(AgeFeature feature, WorldAge ours, WorldAge theirs, bool empire = false)
        {
            return new AgeForm
            {
                feature = feature, ours = ours, theirs = theirs, empire = empire,
                word = AgesMath.WordKey(feature, AgesMath.WordAge(ours, theirs)).Translate().Resolve(),
                rite = AgesMath.RiteKey(feature, AgesMath.RiteAge(ours, theirs)).Translate().Resolve()
            };
        }

        /// <summary>A feature's form between a home and a people.</summary>
        public static AgeForm Form(AgeFeature feature, Map colony, Faction people) => Form(feature, Of(colony), Of(people), IsEmpire(people));
    }
}
