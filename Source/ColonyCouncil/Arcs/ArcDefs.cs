using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>How often personal stories begin (a setting).</summary>
    public enum ArcFrequency
    {
        Off,
        Rare,
        Normal,
        Frequent
    }

    /// <summary>What moves a personal story on from one stage to the next.</summary>
    public enum ArcGateKind
    {
        /// <summary>Days in the stage.</summary>
        Time,
        /// <summary>A condition (<see cref="ArcHold"/>) holding for a number of hours.</summary>
        Hold,
        /// <summary>A skill level reached (theirs, or the other person's).</summary>
        Skill,
        /// <summary>Experience earned in the story's skill: work done.</summary>
        Practice,
        /// <summary>One of the game's records grown (meals cooked, things built...).</summary>
        Record,
        /// <summary>A conversation between them (an arc talk in the social log).</summary>
        Talk,
        /// <summary>Something they made of at least a quality.</summary>
        Made,
        /// <summary>Back on a home map.</summary>
        Home,
        /// <summary>Fought in a fight at home and came through it standing.</summary>
        Battle,
        /// <summary>Bonded with the animal the story is about.</summary>
        Bond,
        /// <summary>Another dream.</summary>
        Dream,
        /// <summary>One of their wishes granted by the council.</summary>
        Granted,
        /// <summary>Their life goal reached.</summary>
        Goal,
        /// <summary>A bond with the other person (friends, rivals...) at least this strong.</summary>
        Tie,
        /// <summary>Whatever the story's worker says (a nemesis brought down, say).</summary>
        Custom
    }

    /// <summary>A condition a Hold gate waits on, checked once an hour.</summary>
    public enum ArcHold
    {
        None,
        /// <summary>Mood at least <c>value</c> (0 to 1).</summary>
        Mood,
        /// <summary>Their opinion of each other at least <c>value</c>, both ways.</summary>
        Opinion,
        /// <summary>No fresh insult, slight or fight between them in the last two days.</summary>
        NoFriction,
        /// <summary>Near the other person (or animal): the same room, or a few steps apart.</summary>
        Near,
        /// <summary>On the same map as the other person.</summary>
        SameMap,
        /// <summary>A bedroom of their own.</summary>
        OwnRoom,
        /// <summary>The heart's need for belonging at least <c>value</c> (0 to 100); never while feelings are off.</summary>
        Belonging,
        /// <summary>Strain at most <c>value</c>; never while feelings are off.</summary>
        Strain,
        /// <summary>Trust in the council at least <c>value</c> (-100 to 100).</summary>
        Trust,
        /// <summary>On a home map.</summary>
        AtHome
    }

    /// <summary>
    /// What a stage waits for before the story moves on. Every gate waits at least <see cref="minDays"/>; after
    /// <see cref="timeoutDays"/> the story either moves on anyway (<see cref="timeoutPasses"/>: time did it) or fizzles.
    /// Any gate in <see cref="or"/> opens it too.
    /// </summary>
    public class ArcGate
    {
        public ArcGateKind kind = ArcGateKind.Time;
        public float minDays = 1f;
        /// <summary>Time: days in the stage.</summary>
        public float days = 3f;
        /// <summary>Hold: the condition, its value, and how many hours it must hold (see ArcMath.Held).</summary>
        public ArcHold hold = ArcHold.None;
        public float value;
        public float hours = 24f;
        /// <summary>Skill, Practice: the other person's skill instead (a student's).</summary>
        public bool onOther;
        /// <summary>Skill: levels above where they were when the stage began...</summary>
        public int gain = 1;
        /// <summary>...or at least this level, whichever is higher.</summary>
        public int level;
        /// <summary>Practice: experience; Record: how much more; Tie: strength.</summary>
        public float amount = 4000f;
        /// <summary>Record: the RecordDef's name.</summary>
        public string record;
        /// <summary>Made: the least quality that counts.</summary>
        public QualityCategory quality = QualityCategory.Excellent;
        /// <summary>Talk: the conversation (an InteractionDef with ColonyCouncil.InteractionWorker_ArcTalk).</summary>
        public InteractionDef talk;
        /// <summary>Talk: with anyone who thinks well of them, not only the story's other person.</summary>
        public bool anyone;
        /// <summary>Talk: only while their opinions of each other are at least this, both ways.</summary>
        public int minOpinion = -100;
        /// <summary>
        /// Talk: after this many days in the stage with the two on the same map, it counts as had (a conversation the
        /// log didn't catch). 0: only a real one counts.
        /// </summary>
        public float quietAfterDays = 7f;
        /// <summary>Tie: the kind of bond.</summary>
        public TieKind tie = TieKind.Friends;
        public float timeoutDays = 30f;
        public bool timeoutPasses;
        public List<ArcGate> or;

        public IEnumerable<string> ConfigErrors(string where)
        {
            if (kind == ArcGateKind.Talk)
            {
                if (talk == null) yield return where + ": a Talk gate needs a talk";
                else if (!(talk.Worker is InteractionWorker_ArcTalk)) yield return where + ": talk " + talk.defName + " must use ColonyCouncil.InteractionWorker_ArcTalk";
            }
            if (kind == ArcGateKind.Hold && hold == ArcHold.None) yield return where + ": a Hold gate needs a hold condition";
            if (kind == ArcGateKind.Record && record.NullOrEmpty()) yield return where + ": a Record gate needs a record";
            if (minDays < 0f) yield return where + ": minDays below zero";
            if (or != null)
                foreach (var g in or)
                {
                    if (g == null) continue;
                    if (g.or != null) yield return where + ": an alternative gate can't have alternatives of its own";
                    foreach (var e in g.ConfigErrors(where + " (or)")) yield return e;
                }
        }
    }

    /// <summary>
    /// One stage of a personal story. On reaching it: its effects, a journal line in the colonist's own voice (and one
    /// from the other person, if written), a chronicle line and a message. Its gate says what moves the story on; the
    /// last stage has none, and reaching it ends the story well.
    /// </summary>
    public class ArcStage
    {
        /// <summary>A short name for the stage, for the Story tab ("home again").</summary>
        [MustTranslate] public string label;
        /// <summary>What the story waits for, for the Story tab ({PAWN_...}, {OTHER_...} and the story's values).</summary>
        [MustTranslate] public string waiting;
        public ToneLines journal = new ToneLines();
        public ToneLines otherJournal;
        /// <summary>Third person, for the chronicle; one is picked.</summary>
        [MustTranslate] public List<string> chronicle;
        public List<DilemmaEffect> effects;
        public ArcGate gate;
    }

    /// <summary>
    /// A personal story: a few stages in one colonist's life, over days to a season, that start from what Rimfolk
    /// already knows about them (their losses, rivals, secrets, skills, feelings...) and move on as things happen in
    /// play. The worker finds who it could be about; the stages say what happens. Texts may use {PAWN_...} (the
    /// colonist), {OTHER_...} (the other person, or the animal), {PET_...} (the animal), {SKILL}, {LOST} (someone they
    /// lost), {NAME} (an enemy's name), {FACTION}, {GOAL}, {QUIRK} and {DAYS}; lines may carry the usual tags plus the
    /// story's own ([ex], [kin], [betrayed], [nemesis], [quirk], [child], [timeout], [otherdead]...).
    /// </summary>
    public class ArcDef : Def
    {
        public Type workerClass = typeof(ArcWorker);
        public float weight = 1f;
        /// <summary>Who the story can be about (traits, passions, mood, age).</summary>
        public DilemmaRole who;
        /// <summary>Children can have it.</summary>
        public bool children;
        /// <summary>One line on what the story is, for the Almanac and the Story tab's tooltip.</summary>
        [MustTranslate] public string summary;
        public List<ArcStage> stages = new List<ArcStage>();
        /// <summary>The whole story may take at most this long; after that it fizzles.</summary>
        public float maxDays = 60f;
        /// <summary>
        /// The other person is what the story is about (a rival, a teacher): if they die or leave, it fizzles. Off for a
        /// story that only leans on a friend to talk to, which goes on without them.
        /// </summary>
        public bool needsOther = true;

        // ---- What the workers look for (each worker reads the ones it needs) ----

        /// <summary>Memories (ThoughtDef names) about someone that can start it.</summary>
        public List<string> memories;
        public float memoryDays = 20f;
        /// <summary>The skills it can be about.</summary>
        public List<SkillDef> skills;
        public int minLevel;
        public int maxLevel = 20;
        /// <summary>How good a teacher or elder must be.</summary>
        public int masterLevel = 12;
        /// <summary>Quirks (QuirkDef names) that can start it.</summary>
        public List<string> quirks;

        // ---- When it doesn't end well ----

        /// <summary>Journal when it fizzles (tagged [timeout], [otherdead], [otherleft], [gone] or the worker's own).</summary>
        public ToneLines fizzle = new ToneLines();
        [MustTranslate] public List<string> fizzleChronicle;
        public List<DilemmaEffect> fizzleEffects;
        /// <summary>
        /// When the colonist dies before the story ends: a line written by the other person (or whoever was closest) as
        /// {PAWN}, about the dead as {OTHER}.
        /// </summary>
        public ToneLines eulogy;

        [Unsaved] private ArcWorker worker;

        public ArcWorker Worker
        {
            get
            {
                if (worker == null)
                {
                    worker = (ArcWorker)Activator.CreateInstance(workerClass ?? typeof(ArcWorker));
                    worker.def = this;
                }
                return worker;
            }
        }

        public ArcStage Stage(int i) => stages != null && i >= 0 && i < stages.Count ? stages[i] : null;

        public int LastStage => (stages?.Count ?? 0) - 1;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            if (workerClass == null || !typeof(ArcWorker).IsAssignableFrom(workerClass)) yield return "workerClass must be an ArcWorker";
            if (stages == null || stages.Count < 3 || stages.Count > 4) yield return "a personal story has 3 or 4 stages";
            if (summary.NullOrEmpty()) yield return "no summary";
            if (stages == null) yield break;
            for (int i = 0; i < stages.Count; i++)
            {
                var s = stages[i];
                if (s == null)
                {
                    yield return "stage " + i + " is empty";
                    continue;
                }
                if (s.label.NullOrEmpty()) yield return "stage " + i + " has no label";
                if (s.journal == null || !s.journal.Any) yield return "stage " + i + " has no journal lines";
                if (s.chronicle.NullOrEmpty()) yield return "stage " + i + " has no chronicle lines";
                bool last = i == stages.Count - 1;
                if (!last && s.gate == null) yield return "stage " + i + " has no gate";
                if (last && s.gate != null) yield return "the last stage ends the story and has no gate";
                if (s.gate != null)
                    foreach (var e in s.gate.ConfigErrors("stage " + i)) yield return e;
            }
            if (fizzle == null || !fizzle.Any) yield return "no fizzle lines";
        }
    }
}
