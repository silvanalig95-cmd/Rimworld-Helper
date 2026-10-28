using System.Collections.Generic;
using System.Linq;

namespace ColonyCouncil
{
    /// <summary>
    /// Whether a Hospitality guest would stay of their own will (Integrations/GuestWill.cs), free of game types so
    /// Tools/Tests can cover it. They need a reason to stay (love or family here, hard times at home, a stay that's going
    /// well, the colony's faith); there has to be room for one more (a bed, a room of their own for some, food in store);
    /// and what someone of their passions and nature would want of a home has to be there. Short of that, Hospitality
    /// offers only to recruit them by force. What holds them at home (a partner or young children there, their parents
    /// while they're young and not in love here, their people's leader, a faith they hold fast that isn't the colony's)
    /// never bars them: each tie asks for more friends here, and each reason to stay beyond the first makes up for one.
    /// What taking them costs their people goes by who they are, and their family may come to ask for them back.
    /// </summary>
    public static class GuestWillMath
    {
        /// <summary>They hold their faith this fast (certainty) and it isn't the colony's: it ties them to home.</summary>
        public const float DevoutCertainty = 0.85f;

        /// <summary>Under this age, parents at home tie them to home (unless they've fallen in love here).</summary>
        public const int YoungAge = 25;

        /// <summary>A visit going this well (Hospitality's own score, 0 to 1) is a reason to stay.</summary>
        public const float GoodStay = 0.75f;

        /// <summary>Thinking this well of someone here they're drawn to counts as love here.</summary>
        public const float SmittenOpinion = 75f;

        /// <summary>Days of food in store wanted for the colony and one more, at this much nutrition a day each.</summary>
        public const float FoodDays = 4f, NutritionPerDay = 1.6f;

        /// <summary>How impressive a room has to be for a good kitchen, or a well-built room ("decent").</summary>
        public const float DecentRoom = 30f;

        /// <summary>What each want asks for.</summary>
        public const int FieldCells = 150, ArtPieces = 3, TameAnimals = 3, Comrades = 3, ComradeSkill = 8, FineMeals = 10, GoodRooms = 3, MedicalBeds = 2;

        /// <summary>At most this many wants each.</summary>
        public const int MaxWants = 2;

        /// <summary>A colonist who thinks this badly of the guest objects to taking them in.</summary>
        public const float DislikeOpinion = -20f;

        /// <summary>The wants, by name (their texts are CC_GuestWill_Want_*).</summary>
        public static class Want
        {
            public const string Kitchen = "kitchen", Fields = "fields", Hospital = "hospital", Art = "art", Study = "study", Workshop = "workshop",
                Animals = "animals", Rec = "rec", Comrades = "comrades", Rooms = "rooms", Food = "food";
        }

        /// <summary>The want a passion for each skill brings.</summary>
        private static readonly Dictionary<string, string> BySkill = new Dictionary<string, string>
        {
            { "Cooking", Want.Kitchen }, { "Plants", Want.Fields }, { "Medicine", Want.Hospital }, { "Artistic", Want.Art },
            { "Intellectual", Want.Study }, { "Crafting", Want.Workshop }, { "Animals", Want.Animals }, { "Social", Want.Rec },
            { "Shooting", Want.Comrades }, { "Melee", Want.Comrades }, { "Construction", Want.Rooms }
        };

        /// <summary>How many of what a want asks for (fields: cells, a hospital: medical beds...), or 0 for a want that's there or not.</summary>
        public static int Need(string want)
        {
            switch (want)
            {
                case Want.Fields: return FieldCells;
                case Want.Hospital: return MedicalBeds;
                case Want.Art: return ArtPieces;
                case Want.Animals: return TameAnimals;
                case Want.Comrades: return Comrades;
                case Want.Rooms: return GoodRooms;
                case Want.Food: return FineMeals;
                default: return 0;
            }
        }

        /// <summary>Their parents at home tie them to home: only while they're young, and not once they've fallen in love here.</summary>
        public static bool ParentsHold(float age, bool loveHere) => age < YoungAge && !loveHere;

        /// <summary>
        /// What they want of a home, at most <see cref="MaxWants"/>: a gourmand good food first, then by their passions,
        /// burning before minor and the skill they're best at first. Nothing for a child, or for an ascetic.
        /// </summary>
        public static List<string> Wants(IEnumerable<(string skill, int passion, int level)> skills, ICollection<string> traits, bool child)
        {
            var wants = new List<string>();
            if (child || traits != null && traits.Contains("Ascetic")) return wants;
            if (traits != null && traits.Contains("Gourmand")) wants.Add(Want.Food);
            if (skills != null)
                foreach (var s in skills.Where(s => s.passion > 0).OrderByDescending(s => s.passion).ThenByDescending(s => s.level))
                {
                    if (wants.Count >= MaxWants) break;
                    if (s.skill != null && BySkill.TryGetValue(s.skill, out var w) && !wants.Contains(w)) wants.Add(w);
                }
            return wants;
        }

        /// <summary>They'd want a room of their own: the greedy, the jealous and the titled, never an ascetic.</summary>
        public static bool WantsOwnRoom(ICollection<string> traits, bool titled) =>
            (traits == null || !traits.Contains("Ascetic")) && (titled || traits != null && (traits.Contains("Greedy") || traits.Contains("Jealous")));

        /// <summary>Days the food in store lasts this many eaters.</summary>
        public static float DaysOfFood(float nutrition, int eaters) => eaters <= 0 ? 999f : nutrition / (eaters * NutritionPerDay);

        /// <summary>They'd stay of their own will, given the friends here that what holds them at home asks for (<see cref="ExtraFriends"/>).</summary>
        public static bool Willing(int reasons, bool room, int wantsUnmet) => reasons > 0 && room && wantsUnmet == 0;

        /// <summary>The ties at home, by name (their texts are CC_GuestWill_Tie_*).</summary>
        public static class Tie
        {
            public const string Partner = "Partner", Child = "Child", Children = "Children", Parent = "Parent", Leader = "Leader", LeaderKin = "LeaderKin", Faith = "Faith";
        }

        /// <summary>How many more friends here a tie asks for: two for a partner or young children at home, two to lead their people, else one.</summary>
        public static int TieWeight(string tie) =>
            tie == Tie.Partner || tie == Tie.Child || tie == Tie.Children || tie == Tie.Leader ? 2 : tie == Tie.Parent || tie == Tie.LeaderKin || tie == Tie.Faith ? 1 : 0;

        /// <summary>The friends here asked for beyond Hospitality's own, for what holds them at home: each reason to stay beyond the first makes up for one.</summary>
        public static int ExtraFriends(int tieWeight, int reasons)
        {
            int off = reasons > 1 ? reasons - 1 : 0;
            return tieWeight > off ? tieWeight - off : 0;
        }

        /// <summary>
        /// What taking them costs their people, as a factor on Hospitality's own price: less for someone with no family
        /// among them, more for someone whose family is there, twice for their leader's family, more for one who holds a
        /// role of their faith.
        /// </summary>
        public static float CostFactor(bool familyThere, bool leaderKin, bool role)
        {
            float f = familyThere ? 1.3f : 0.7f;
            if (leaderKin) f *= 2f;
            if (role) f *= 1.3f;
            return f < 0.5f ? 0.5f : f > 2.5f ? 2.5f : f;
        }

        /// <summary>Days after joining before their family may come to ask for them back, and how long they may still come.</summary>
        public const int AskMinDays = 10, AskMaxDays = 30, AskWindowDays = 15;

        /// <summary>Goodwill their people give for letting them go home, and take for keeping them.</summary>
        public const int HomeGoodwill = 10, KeptGoodwill = 15;

        /// <summary>
        /// How likely their family comes to ask for them back: a partner most, then a parent, a grown child, a brother or
        /// sister; more the more they think of them, more again when they were taken by force.
        /// </summary>
        public static float AskChance(string relation, float opinion, bool forced)
        {
            float c;
            switch (relation)
            {
                case "partner": c = 0.9f; break;
                case "parent": c = 0.6f; break;
                case "child": c = 0.5f; break;
                case "sibling": c = 0.35f; break;
                default: return 0f;
            }
            c += (opinion < 0f ? 0f : opinion > 100f ? 100f : opinion) / 250f;
            if (forced) c *= 1.3f;
            return c > 1f ? 1f : c;
        }

        /// <summary>The order family comes in: a partner first, then a parent, a grown child, a brother or sister.</summary>
        public static int KinRank(string relation) => relation == "partner" ? 0 : relation == "parent" ? 1 : relation == "child" ? 2 : relation == "sibling" ? 3 : 9;

        /// <summary>How much they want to go home with them (MovingOnMath.Family's measure): a partner as love, others as family.</summary>
        public static float Longing(string relation, float opinion) =>
            relation == "partner" ? MovingOnMath.Love(false, false, opinion) : MovingOnMath.Family(opinion, relation == "child");
    }
}
