using System;
using ColonyCouncil;

/// <summary>
/// What a reached life goal leaves behind (Aspirations/GoalRewardMath): the skill it was about, found from its own
/// skill, its record, its counter, its office or its kind, and the inspiration that fits.
/// </summary>
public static class GoalRewardTests
{
    public static int Run(Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "life goal rewards: " + what);
        }

        C(GoalRewardMath.SkillFor("Social", "MealsCooked", null, null, null) == "Social", "a goal's own skill comes first");
        C(GoalRewardMath.SkillFor(null, "MealsCooked", null, null, null) == "Cooking", "five hundred meals is a cook's goal");
        C(GoalRewardMath.SkillFor(null, "OperationsPerformed", null, null, null) == "Medicine" && GoalRewardMath.SkillFor(null, "Council_BirthsAttended", null, null, null) == "Medicine",
            "operations and births are a healer's");
        C(GoalRewardMath.SkillFor(null, "KillsHumanlikes", null, null, null) == GoalRewardMath.Combat, "enemies brought down: whichever they fight with");
        C(GoalRewardMath.SkillFor(null, null, "madeFine", null, null) == GoalRewardMath.CraftOrArt, "one last thing worth keeping: crafted or made as art");
        C(GoalRewardMath.SkillFor(null, null, null, "Council_Office_Physician", null) == "Medicine", "the physician's office is a healer's");
        C(GoalRewardMath.SkillFor(null, null, null, null, "AspirationWorker_GreatBeast") == "Animals", "a great beast tamed is a tamer's");
        C(GoalRewardMath.SkillFor(null, null, null, null, "AspirationWorker_Spouse") == null && GoalRewardMath.SkillFor(null, "CorpsesBuried", null, null, null) == null,
            "a marriage or a burial is no skill's");

        C(GoalRewardMath.InspirationFor("Medicine", false) == "Inspired_Surgery" && GoalRewardMath.InspirationFor("Animals", false) == "Inspired_Taming",
            "a healer is inspired to surgery, a tamer to taming");
        C(GoalRewardMath.InspirationFor("Social", true) == "Inspired_Recruitment" && GoalRewardMath.InspirationFor("Social", false) == "Inspired_Trade",
            "winning prisoners over inspires recruiting; other talk, trade");
        C(GoalRewardMath.InspirationFor("Crafting", false) == "Inspired_Creativity" && GoalRewardMath.InspirationFor("Construction", false) == "Inspired_Creativity",
            "makers and builders are inspired to make something fine");
        C(GoalRewardMath.InspirationFor("Shooting", false) == "Frenzy_Shoot" && GoalRewardMath.InspirationFor("Melee", false) == "Frenzy_Go", "fighters get a fighter's frenzy");
        C(GoalRewardMath.InspirationFor("Cooking", false) == "Frenzy_Work" && GoalRewardMath.InspirationFor(null, false) == "Frenzy_Work", "anything else: a work frenzy");
        C(GoalRewardMath.MarkLearnFactor > 1f && GoalRewardMath.MarkLearnFactor < 1.5f, "a marked skill comes a little faster, not a lot");
        return n;
    }
}
