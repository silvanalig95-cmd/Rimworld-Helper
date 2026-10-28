using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// What a reached life goal leaves behind (Aspirations/GoalRewards.cs), by names so Tools/Tests can check it: the
    /// skill a goal was about (its own skill, else what its record or counter counts, else the work of its office or
    /// kind), the inspiration that fits it, and how much faster a marked skill is learned.
    /// </summary>
    public static class GoalRewardMath
    {
        /// <summary>A marked skill is learned this much faster (it never slips at all).</summary>
        public const float MarkLearnFactor = 1.15f;

        /// <summary>Stands for "Shooting or Melee, whichever they're better at", and "Crafting or Artistic".</summary>
        public const string Combat = "combat", CraftOrArt = "craftart";

        private static readonly Dictionary<string, string> ByRecord = new Dictionary<string, string>
        {
            { "MealsCooked", "Cooking" }, { "ThingsConstructed", "Construction" }, { "TimesTendedOther", "Medicine" },
            { "OperationsPerformed", "Medicine" }, { "Council_PatientsPulledThrough", "Medicine" }, { "Council_BirthsAttended", "Medicine" },
            { "AnimalsTamed", "Animals" }, { "PrisonersRecruited", "Social" }, { "CellsMined", "Mining" },
            { "ResearchPointsResearched", "Intellectual" }, { "KillsAnimals", "Shooting" }, { "PlantsHarvested", "Plants" },
            { "ThingsCrafted", "Crafting" }, { "KillsHumanlikes", Combat }, { "KillsMechanoids", Combat }
        };

        private static readonly Dictionary<string, string> ByCounter = new Dictionary<string, string>
        {
            { "madeFine", CraftOrArt }, { "stranger", "Social" }
        };

        private static readonly HashSet<string> AnimalWorkers = new HashSet<string>
        {
            "AspirationWorker_Champion", "AspirationWorker_GreatBeast", "AspirationWorker_Herd", "AspirationWorker_TrainFully",
            "AspirationWorker_OwnAnimal", "AspirationWorker_Pokedex", "AspirationWorker_PokemonLevel"
        };

        /// <summary>
        /// The skill a goal was about, by name (or <see cref="Combat"/>, <see cref="CraftOrArt"/>), or null for a goal of the
        /// heart (a marriage, friends, years): its own skill first, then its record, counter, office and kind.
        /// </summary>
        public static string SkillFor(string skill, string record, string counter, string office, string worker)
        {
            if (!string.IsNullOrEmpty(skill)) return skill;
            if (record != null && ByRecord.TryGetValue(record, out var r)) return r;
            if (counter != null && ByCounter.TryGetValue(counter, out var c)) return c;
            if (office != null)
            {
                if (office.Contains("Physician")) return "Medicine";
                if (office.Contains("Marshal")) return Combat;
                if (office.Contains("Steward")) return "Social";
            }
            if (worker != null && AnimalWorkers.Contains(worker)) return "Animals";
            return null;
        }

        /// <summary>The inspiration that fits a goal about this skill (none: a work frenzy).</summary>
        public static string InspirationFor(string skill, bool winningPeopleOver)
        {
            switch (skill)
            {
                case "Medicine": return "Inspired_Surgery";
                case "Animals": return "Inspired_Taming";
                case "Social": return winningPeopleOver ? "Inspired_Recruitment" : "Inspired_Trade";
                case "Artistic":
                case "Crafting":
                case "Construction": return "Inspired_Creativity";
                case "Shooting": return "Frenzy_Shoot";
                case "Melee": return "Frenzy_Go";
                default: return "Frenzy_Work";
            }
        }
    }
}
