namespace ColonyCouncil
{
    /// <summary>The features that take the form of the age (ROADMAP.md, "Every age, not only the medieval one").</summary>
    public enum AgeFeature
    {
        /// <summary>A favor owed.</summary>
        Favor,
        /// <summary>A pact or a truce.</summary>
        Pact,
        /// <summary>A feast or a contest with guests.</summary>
        Feast,
        /// <summary>A marriage alliance.</summary>
        Marriage,
        /// <summary>A ward or a hostage.</summary>
        Ward,
        Ransom,
        /// <summary>Fealty and vassals.</summary>
        Fealty,
        /// <summary>The council's tasks.</summary>
        CouncilTasks,
        /// <summary>A house's renown.</summary>
        Renown,
        Schemes,
        /// <summary>A new town founded: the word is how the colony hears of it (its means), the rite how their people found one (World/Region/).</summary>
        Founding,
        /// <summary>A trading post: the word is how the colony goes there to trade (its means), the rite what their people's post is (World/Region/TradingPost.cs).</summary>
        Post
    }

    /// <summary>
    /// The ages' small rules (Ages.cs is the game's side): the tag of an age as lines are tagged, the keyed strings of each
    /// feature's form, and the rule for two peoples of different ages. Pure (no game types): Tools/Tests checks them.
    /// <para>
    /// The rule for mixed ages: the colony's own age sets its means, the word it has for the thing and what it does it
    /// with (a debt of honor, an IOU, a ledger entry; a runner or a comms console; a feast hall or a gala); the other
    /// people's age sets the rite, how it is done with them by their ways (a gift owed back, oaths before witnesses, a
    /// registered treaty). With both of one age, word and rite are that age's.
    /// </para>
    /// </summary>
    public static class AgesMath
    {
        public static readonly WorldAge[] All = { WorldAge.Tribal, WorldAge.Medieval, WorldAge.Industrial, WorldAge.Spacer };

        public static readonly AgeFeature[] Features =
        {
            AgeFeature.Favor, AgeFeature.Pact, AgeFeature.Feast, AgeFeature.Marriage, AgeFeature.Ward, AgeFeature.Ransom, AgeFeature.Fealty,
            AgeFeature.CouncilTasks, AgeFeature.Renown, AgeFeature.Schemes, AgeFeature.Founding, AgeFeature.Post
        };

        /// <summary>An age as lines are tagged: tribal, medieval, industrial, spacer (Flavor.AddEraTags, LineTags).</summary>
        public static string Tag(WorldAge age)
        {
            switch (age)
            {
                case WorldAge.Tribal: return "tribal";
                case WorldAge.Medieval: return "medieval";
                case WorldAge.Industrial: return "industrial";
                default: return "spacer";
            }
        }

        /// <summary>The age a word comes from: the colony's own (its means).</summary>
        public static WorldAge WordAge(WorldAge ours, WorldAge theirs) => ours;

        /// <summary>The age a rite comes from: the other people's (their ways).</summary>
        public static WorldAge RiteAge(WorldAge ours, WorldAge theirs) => theirs;

        /// <summary>The key of a feature's word in an age: CC_Form_Favor_Tribal.</summary>
        public static string WordKey(AgeFeature feature, WorldAge age) => "CC_Form_" + feature + "_" + age;

        /// <summary>The key of a feature's rite in an age: CC_Form_Favor_Tribal_Rite.</summary>
        public static string RiteKey(AgeFeature feature, WorldAge age) => WordKey(feature, age) + "_Rite";
    }
}
