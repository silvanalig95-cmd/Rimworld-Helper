using System;
using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Diplomatic Marriage+ (Ionfrigate12345.DiplomaticMarriagePlus), read by name from its source (GitHub,
    /// Ionfrigate12345/Rimworld_DiplomaticMarriagePlus) and its assembly: the permanent alliance its marriage makes, as a
    /// tie of kind marriage for the world ledger (a view). The alliance is the world component
    /// <c>DiplomaticMarriagePlus.Model.PermanentAlliance</c>, with the properties <c>WithFaction</c> (the allied people),
    /// <c>PlayerBetrothed</c> (the colony's child who married) and <c>NpcMarriageSeeker</c> (their spouse); the mod sets
    /// all three to null when the alliance ends (its hourly check), so it is in force while they are set. Only these
    /// properties are read: its <c>IsValid</c> also changes the alliance (it names the colony's leader again), so it isn't
    /// called. The tie runs from the allied people (they proposed) to the colony, about the child, with their spouse in
    /// its key ("spouse:p123"). Nothing of the mod's own system is used or changed.
    /// </summary>
    public sealed class MarriageAllianceAdapter : WorldAdapter
    {
        public const string PackageId = "ionfrigate12345.diplomaticmarriageplus";

        private static bool looked;
        private static Type alliance;
        private static PropertyInfo with, child, spouse;

        public override string Name => "Diplomatic Marriage+";

        private static PropertyInfo Property(string name, Type type)
        {
            var p = alliance?.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            return p != null && p.PropertyType == type && p.GetGetMethod() != null ? p : null;
        }

        public override bool Ready()
        {
            if (!looked)
            {
                looked = true;
                if (People.ModActive(PackageId))
                {
                    alliance = GenTypes.GetTypeInAnyAssembly("DiplomaticMarriagePlus.Model.PermanentAlliance");
                    if (alliance != null && !typeof(WorldComponent).IsAssignableFrom(alliance)) alliance = null;
                    with = Property("WithFaction", typeof(Faction));
                    child = Property("PlayerBetrothed", typeof(Pawn));
                    spouse = Property("NpcMarriageSeeker", typeof(Pawn));
                }
            }
            return alliance != null && with != null && child != null && spouse != null;
        }

        public override void Ties(List<Tie> into)
        {
            var comp = Find.World?.GetComponent(alliance);
            if (comp == null) return;
            var people = with.GetValue(comp) as Faction;
            var ours = child.GetValue(comp) as Pawn;
            var theirs = spouse.GetValue(comp) as Pawn;
            var colony = Refs.Seat;
            if (people == null || people.IsPlayer || ours == null || theirs == null || colony.IsNone) return;
            into.Add(new Tie
            {
                kind = Tie.Kind.Marriage, from = Refs.Of(people), to = colony, terms = new TieTerms { about = Refs.Of(ours), key = "spouse:" + Refs.Of(theirs) },
                since = Tie.Never, until = Tie.Never, state = Tie.State.Open, source = "marriage:alliance"
            });
        }
    }
}
