using System;
using ColonyCouncil;

/// <summary>The herd's year and the beast that stays (Animals/HerdStoryMath.cs).</summary>
public static class HerdStoryTests
{
    public static int Run(Action<bool, string> check)
    {
        int n = 0;
        void Check(bool ok, string what)
        {
            n++;
            check(ok, what);
        }

        Check(HerdStoryMath.LeaderScore(6f, 6, 8, false) > HerdStoryMath.LeaderScore(3f, 6, 8, false), "the elder leads");
        Check(HerdStoryMath.LeaderScore(4f, 6, 8, false) > HerdStoryMath.LeaderScore(4f, 1, 8, false), "one of the many leads before the lone goat");
        Check(HerdStoryMath.LeaderScore(2f, 2, 8, true) > HerdStoryMath.LeaderScore(7f, 6, 8, false), "the old leader's blood comes first");
        Check(HerdStoryMath.LeaderScore(-1f, 0, 0, false) == 0f, "nothing below nothing");
        Check(HerdStoryMath.Generation(1) == 2 && HerdStoryMath.Generation(0) == 2, "a newborn is one generation on from its mother, and never before the second");
        Check(HerdStoryMath.GenerationNews(2, 1) && !HerdStoryMath.GenerationNews(2, 2) && !HerdStoryMath.GenerationNews(1, 0), "a generation is news the first time it's reached, from the second on");
        Check(HerdStoryMath.SpringCalf(true, 5503, 5502) && !HerdStoryMath.SpringCalf(true, 5503, 5503) && !HerdStoryMath.SpringCalf(false, 5503, 5502), "one first calf a spring, in spring");

        // Words by the animal's kind (the player's save: a chick called the first calf of spring, an old chicken gray at the muzzle).
        Check(HerdStoryMath.BodyOf("Bird", false) == HerdStoryMath.HerdBody.Bird, "a chicken's body is a bird's");
        Check(HerdStoryMath.BodyOf("QuadrupedAnimalWithHooves", false) == HerdStoryMath.HerdBody.Beast && HerdStoryMath.BodyOf("QuadrupedAnimalWithPaws", false) == HerdStoryMath.HerdBody.Beast
              && HerdStoryMath.BodyOf("Monkey", false) == HerdStoryMath.HerdBody.Beast, "hooves, paws and a monkey are beasts");
        Check(HerdStoryMath.BodyOf("Snake", false) == HerdStoryMath.HerdBody.Other && HerdStoryMath.BodyOf("TurtleLike", false) == HerdStoryMath.HerdBody.Other
              && HerdStoryMath.BodyOf("QuadrupedAnimalWithPaws", true) == HerdStoryMath.HerdBody.Other && HerdStoryMath.BodyOf(null, false) == HerdStoryMath.HerdBody.Other,
            "a snake, a turtle, an insect and the unknown are none of those");
        Check(HerdStoryMath.BodyTag(HerdStoryMath.HerdBody.Bird) == "body:bird" && HerdStoryMath.BodyTag(HerdStoryMath.HerdBody.Beast) == "body:beast", "the body's tag for the journal");
        Check(HerdStoryMath.YoungWord("chick", "chicken") == "chick" && HerdStoryMath.YoungWord(" calf ", "cow") == "calf", "the young by their kind's own word");
        Check(HerdStoryMath.YoungWord(null, "chicken") == null && HerdStoryMath.YoungWord("", "chicken") == null && HerdStoryMath.YoungWord("Boomalope", "boomalope") == null,
            "no word of its own for the young: the first born");
        {
            string root = AppContext.BaseDirectory;
            while (root != null && !Directory.Exists(Path.Combine(root, "Defs"))) root = Path.GetDirectoryName(root);
            if (root != null)
            {
                string herd = File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", "Animals", "HerdStory.cs"));
                Check(!herd.Contains("\"CC_Herd_SpringCalfTitle\"") && !herd.Contains("\"CC_Herd_SpringCalf\"") && herd.Contains("AnimalFacts.YoungWord(baby)"),
                    "the first born of spring is called by its kind's word, never a calf for every kind");
                Check(herd.Contains("\"CC_Herd_LastSeason_Bird\"") && herd.Contains("\"CC_Herd_Leader_Pens\""), "old age and the leader's ways in the body's words");
                string kinds = File.ReadAllText(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_HerdStory_Kinds.xml"));
                Check(!kinds.Contains("calf") && !kinds.Contains("muzzle") && !kinds.Contains("this morning"), "the kinds' words say no calf, no muzzle, no hour they don't know");
            }
        }

        Check(HerdStoryMath.BeastFits(0.8f, true, false, false), "a wolf fits");
        Check(!HerdStoryMath.BeastFits(0.3f, true, false, false), "a fox is too small to be the beast");
        Check(HerdStoryMath.BeastFits(2f, false, false, false) && !HerdStoryMath.BeastFits(1f, false, false, false), "a stag fits, a hare doesn't");
        Check(!HerdStoryMath.BeastFits(3f, true, false, true) && !HerdStoryMath.BeastFits(3f, false, true, false), "great beasts and insects are other stories");
        Check(!HerdStoryMath.AskDue(3f, 0, 0f) && HerdStoryMath.AskDue(4f, 0, 0f), "the first question four days after the naming");
        Check(!HerdStoryMath.AskDue(10f, 1, 5f) && HerdStoryMath.AskDue(10f, 1, 8f), "and again after a gap");
        Check(!HerdStoryMath.AskDue(40f, HerdStoryMath.MaxAsks, 40f), "three questions at most");
        Check(HerdStoryMath.Stance(1, true) == "fed" && HerdStoryMath.Stance(0, true) == "feared" && HerdStoryMath.Stance(0, false) == "watched", "fed outweighs feared, else watched");

        Check(HerdStoryMath.PokemonFits(0.3f) && !HerdStoryMath.PokemonFits(0.2f), "a small Pokémon still counts, the tiniest don't");
        Check(HerdStoryMath.StealDue("watched", 1f, 1f) && HerdStoryMath.StealDue("feared", 3f, 2f), "a watched or feared Pokémon raids the stores from its second day, a day apart");
        Check(!HerdStoryMath.StealDue("fed", 3f, 3f) && !HerdStoryMath.StealDue("watched", 0.5f, 3f) && !HerdStoryMath.StealDue("watched", 3f, 0.5f), "not when fed, not on the first day, not twice a day");
        Check(Math.Abs(HerdStoryMath.ShowWinChance(20, 20) - 0.5f) < 0.001f, "even levels, even odds");
        Check(HerdStoryMath.ShowWinChance(50, 20) == 0.85f && HerdStoryMath.ShowWinChance(10, 60) == 0.15f, "a big gap is never a sure thing either way");
        Check(HerdStoryMath.ShowWinChance(32, 20) > 0.5f && HerdStoryMath.ShowWinChance(32, 20) < 0.85f, "a dozen levels tilt it");
        return n;
    }
}
