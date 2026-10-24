using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>How a personal story ended.</summary>
    public enum ArcOutcome
    {
        Done,
        Fizzled,
        Died,
        Left
    }

    /// <summary>
    /// A personal story under way: whose it is, who else is in it, the stage it has reached and what that stage has seen
    /// so far (where their skills and records stood when it began, a talk, a piece made, a fight).
    /// </summary>
    public class Arc : IExposable
    {
        public ArcDef def;
        public Pawn pawn;
        /// <summary>The other person in it (a rival, a teacher, a friend), or the animal; may be null.</summary>
        public Pawn other;
        public string otherName;
        /// <summary>
        /// A third person, in a story of three (a rival for the same affection, a second student, a brother or sister);
        /// null in most stories.
        /// </summary>
        public Pawn third;
        public string thirdName;
        public SkillDef skill;
        public int stage;
        public int startedTick;
        public int stageTick;
        /// <summary>Time spent waiting while its people were away (or the setting was off), when its clocks don't run.</summary>
        public int pausedTicks;
        /// <summary>The same, within the current stage.</summary>
        public int stagePausedTicks;
        /// <summary>The chronicle lines of the stages so far, for the letter that retells it at the end.</summary>
        public List<string> told = new List<string>();
        /// <summary>The colonist's journal line at the last stage, quoted in that letter.</summary>
        public string lastLine;

        // ---- What the current stage has seen ----

        /// <summary>Hours counted toward a Hold gate (ArcMath.Held).</summary>
        public float held;
        /// <summary>Hours counted toward each alternative Hold gate, by position in the gate's list.</summary>
        public List<float> heldOr = new List<float>();
        public int talkedTick = -1;
        /// <summary>Who they talked with last (for a talk with anyone).</summary>
        public Pawn talkedWith;
        /// <summary>The best quality they've made in this stage, as a number (-1: nothing yet).</summary>
        public int madeQuality = -1;
        /// <summary>
        /// The thing itself, the best they made for a stage that waited for something made (kept past the stage, so the
        /// story's end can go back to it: the dream made real, stood in front of).
        /// </summary>
        public Thing madeThing;
        /// <summary>A fight at home went on during this stage while they were there.</summary>
        public bool sawBattle;
        public int baseLevel;
        public int baseOtherLevel;
        public int baseThirdLevel;
        public float baseXp;
        public float baseOtherXp;
        public float baseThirdXp;
        public float baseRecord;
        public int baseGranted;
        public float baseDamage;

        // ---- What it's about ----

        /// <summary>A tag for its lines: ex, kin, betrayed, nemesis, quirk, fear, nightmare...</summary>
        public string tag;

        // ---- What waits for the two to be together (ArcEffect_Cast with together) ----

        /// <summary>Its Together key, the stage and place of the effect in it, and when it gives up (-1: nothing waits).</summary>
        public string waitKey;
        public int waitStage = -1, waitIndex = -1, waitUntil = -1;
        /// <summary>{LOST}: someone they lost, or whom the vendetta is for.</summary>
        public string lostName;
        /// <summary>{NAME}: an enemy's name.</summary>
        public string name;
        /// <summary>A life goal (its def's name) and how it reads ({GOAL}).</summary>
        public string goal;
        public string goalText;
        public Faction faction;
        public string factionName;
        /// <summary>The quirk that started it (its def's name).</summary>
        public string quirk;
        /// <summary>{DAYS}: how long they were away (the long way home).</summary>
        public int awayDays;
        /// <summary>What the worker keeps to know its story by (a camp's or a world event's id).</summary>
        public string key;

        /// <summary>Its def was missing when the game loaded (a mod removed): dropped without a word.</summary>
        [Unsaved] public bool lost;

        public ArcStage Stage => def?.Stage(stage);

        public ArcGate Gate => Stage?.gate;

        public bool OtherIsAnimal => other != null && other.RaceProps != null && other.RaceProps.Animal;

        /// <summary>A story of three, with its third person still in it.</summary>
        public bool HasThird => third != null;

        /// <summary>
        /// Whom the story's lines and effects are about: the other person, or, in a story that only leans on a friend,
        /// whoever they last talked it over with.
        /// </summary>
        public Pawn Partner => def != null && !def.needsOther && talkedWith != null && !talkedWith.Dead ? talkedWith : other;

        public string Label => def?.LabelCap ?? "";

        public void ExposeData()
        {
            bool defLost = false;
            CouncilScribe.LookDef(ref def, "def", ref defLost);
            if (defLost) lost = true;
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_References.Look(ref other, "other");
            Scribe_Values.Look(ref otherName, "otherName");
            Scribe_References.Look(ref third, "third");
            Scribe_Values.Look(ref thirdName, "thirdName");
            CouncilScribe.LookDef(ref skill, "skill");
            Scribe_Values.Look(ref stage, "stage");
            Scribe_Values.Look(ref startedTick, "startedTick");
            Scribe_Values.Look(ref stageTick, "stageTick");
            Scribe_Values.Look(ref pausedTicks, "pausedTicks");
            Scribe_Values.Look(ref stagePausedTicks, "stagePausedTicks");
            Scribe_Collections.Look(ref told, "told", LookMode.Value);
            Scribe_Values.Look(ref held, "held");
            Scribe_Collections.Look(ref heldOr, "heldOr", LookMode.Value);
            Scribe_Values.Look(ref talkedTick, "talkedTick", -1);
            Scribe_References.Look(ref talkedWith, "talkedWith");
            Scribe_Values.Look(ref madeQuality, "madeQuality", -1);
            Scribe_References.Look(ref madeThing, "madeThing");
            Scribe_Values.Look(ref sawBattle, "sawBattle");
            Scribe_Values.Look(ref baseLevel, "baseLevel");
            Scribe_Values.Look(ref baseOtherLevel, "baseOtherLevel");
            Scribe_Values.Look(ref baseThirdLevel, "baseThirdLevel");
            Scribe_Values.Look(ref baseXp, "baseXp");
            Scribe_Values.Look(ref baseOtherXp, "baseOtherXp");
            Scribe_Values.Look(ref baseThirdXp, "baseThirdXp");
            Scribe_Values.Look(ref baseRecord, "baseRecord");
            Scribe_Values.Look(ref baseGranted, "baseGranted");
            Scribe_Values.Look(ref baseDamage, "baseDamage");
            Scribe_Values.Look(ref waitKey, "waitKey");
            Scribe_Values.Look(ref waitStage, "waitStage", -1);
            Scribe_Values.Look(ref waitIndex, "waitIndex", -1);
            Scribe_Values.Look(ref waitUntil, "waitUntil", -1);
            Scribe_Values.Look(ref tag, "tag");
            Scribe_Values.Look(ref lostName, "lostName");
            Scribe_Values.Look(ref name, "name");
            Scribe_Values.Look(ref goal, "goal");
            Scribe_Values.Look(ref goalText, "goalText");
            Scribe_References.Look(ref faction, "faction");
            Scribe_Values.Look(ref factionName, "factionName");
            Scribe_Values.Look(ref quirk, "quirk");
            Scribe_Values.Look(ref awayDays, "awayDays");
            Scribe_Values.Look(ref key, "key");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (heldOr == null) heldOr = new List<float>();
                if (told == null) told = new List<string>();
            }
        }
    }

    /// <summary>A story that has ended, kept by name so it outlives its def and its people (for the Story tab and the limits).</summary>
    public class ArcRecord : IExposable
    {
        public string defName;
        public string label;
        public string pawnId;
        public string pawnName;
        public string otherName;
        /// <summary>The third person, in a story of three.</summary>
        public string thirdName;
        public int startTick;
        public int endTick;
        public ArcOutcome outcome;
        /// <summary>The stage it had reached.</summary>
        public string stageLabel;

        public void ExposeData()
        {
            Scribe_Values.Look(ref defName, "defName");
            Scribe_Values.Look(ref label, "label");
            Scribe_Values.Look(ref pawnId, "pawnId");
            Scribe_Values.Look(ref pawnName, "pawnName");
            Scribe_Values.Look(ref otherName, "otherName");
            Scribe_Values.Look(ref thirdName, "thirdName");
            Scribe_Values.Look(ref startTick, "startTick");
            Scribe_Values.Look(ref endTick, "endTick");
            Scribe_Values.Look(ref outcome, "outcome");
            Scribe_Values.Look(ref stageLabel, "stageLabel");
        }
    }

    /// <summary>A line still to be written about someone who died before their story ended.</summary>
    public class ArcEulogy : IExposable
    {
        public string defName;
        public Pawn dead;
        public Pawn speaker;
        public int dueTick;
        /// <summary>The story's values, for the lines: {LOST}, {NAME}, {GOAL}, {FACTION}, {SKILL} and its tag.</summary>
        public string lostName;
        public string name;
        public string goalText;
        public string factionName;
        public SkillDef skill;
        public string tag;

        public void ExposeData()
        {
            Scribe_Values.Look(ref defName, "defName");
            Scribe_References.Look(ref dead, "dead");
            Scribe_References.Look(ref speaker, "speaker");
            Scribe_Values.Look(ref dueTick, "dueTick");
            Scribe_Values.Look(ref lostName, "lostName");
            Scribe_Values.Look(ref name, "name");
            Scribe_Values.Look(ref goalText, "goalText");
            Scribe_Values.Look(ref factionName, "factionName");
            CouncilScribe.LookDef(ref skill, "skill");
            Scribe_Values.Look(ref tag, "tag");
        }
    }
}
