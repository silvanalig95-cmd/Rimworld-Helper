using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Something a colony talks about (the last raid, the council, the food, a newcomer...), on which each colonist holds
    /// a view from against (-1) to for (+1), kept with <see cref="Persona.SetStance"/> under the topic's defName (or
    /// defName:id for a view about one person). A view starts from the person's character, traits, passions, faith and
    /// life (<see cref="seed"/>, <see cref="leans"/>, the worker's <see cref="TopicWorker.Experience"/>) the first time
    /// it's needed, then moves slowly with what happens and through talk. The worker says when it comes up and what about.
    /// </summary>
    public class TopicDef : Def
    {
        public Type workerClass = typeof(TopicWorker);
        /// <summary>Its talk in the game's own picker (the social log's line names the topic).</summary>
        public InteractionDef interaction;
        /// <summary>The topic in a few words, for the character page and the Almanac ("the last raid").</summary>
        [MustTranslate] public string about;
        /// <summary>A view for it, as the character page says it: "would rather meet raiders at the gate than wait behind walls".</summary>
        [MustTranslate] public string viewFor;
        /// <summary>A view against it: "wants walls and caution, not heroics".</summary>
        [MustTranslate] public string viewAgainst;
        /// <summary>How much it comes up next to the others, before what's going on.</summary>
        public float relevance = 1f;
        /// <summary>News about it stays hot this many days.</summary>
        public float hotDays = 3f;
        /// <summary>Where someone's character puts their first view: per side, the view at a side of 100, in hundredths.</summary>
        public PersonaShifts seed = new PersonaShifts();
        /// <summary>Traits, passions, memes and precepts that lean someone one way.</summary>
        public List<TopicLean> leans = new List<TopicLean>();
        /// <summary>Who brings it up: per side, how much more (in hundredths at a side of 100).</summary>
        public PersonaShifts interest = new PersonaShifts();
        /// <summary>A small personal lean, so two people alike still differ a little (and each stays themselves).</summary>
        public float spread = 0.15f;
        /// <summary>A view per person it's about (each newcomer gets their own).</summary>
        public bool perSubject;
        /// <summary>Nobody teases anyone about this (the dead).</summary>
        public bool noTease;

        [Unsaved] private TopicWorker workerInt;
        [Unsaved] private float[] seedWeights, interestWeights;

        public TopicWorker Worker
        {
            get
            {
                if (workerInt == null)
                {
                    workerInt = (TopicWorker)Activator.CreateInstance(workerClass ?? typeof(TopicWorker));
                    workerInt.def = this;
                }
                return workerInt;
            }
        }

        public float[] SeedWeights => seedWeights ??= Weights(seed);

        public float[] InterestWeights => interestWeights ??= Weights(interest);

        private static float[] Weights(PersonaShifts s)
        {
            var w = new float[PersonaMath.Axes];
            if (s != null)
                foreach (var a in PersonaMath.All)
                    w[(int)a] = s.Of(a);
            return w;
        }

        /// <summary>The key a view is kept under: the defName, or defName:id for a view about one person.</summary>
        public string Key(TalkSubject subject) => perSubject && subject != null && subject.id >= 0 ? defName + ":" + subject.id : defName;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            if (workerClass == null || !typeof(TopicWorker).IsAssignableFrom(workerClass)) yield return "workerClass must be a TopicWorker";
            if (viewFor.NullOrEmpty() || viewAgainst.NullOrEmpty()) yield return "needs viewFor and viewAgainst";
            if (about.NullOrEmpty()) yield return "needs about";
            if (interaction == null) yield return "needs its interaction";
            if (leans != null)
                foreach (var l in leans)
                    if (l == null || (l.trait.NullOrEmpty() && l.skill.NullOrEmpty() && l.meme.NullOrEmpty() && l.precept.NullOrEmpty()) || l.lean == 0f)
                        yield return "a lean without a trait, skill, meme or precept, or without a lean";
        }

        public static List<TopicDef> All => DefDatabase<TopicDef>.AllDefsListForReading;
    }

    /// <summary>
    /// One thing that leans someone's view: a trait (at a degree), a passion for a skill, a meme or precept of their
    /// faith (Ideology). Looked up by name, so a DLC's or mod's that isn't loaded is simply skipped.
    /// </summary>
    public class TopicLean
    {
        public string trait;
        public int degree;
        public string skill;
        public string meme;
        public string precept;
        public float lean;

        [Unsaved] private bool looked;
        [Unsaved] private TraitDef traitDef;
        [Unsaved] private SkillDef skillDef;
        [Unsaved] private MemeDef memeDef;
        [Unsaved] private PreceptDef preceptDef;

        private void Look()
        {
            if (looked) return;
            looked = true;
            if (!trait.NullOrEmpty()) traitDef = DefDatabase<TraitDef>.GetNamedSilentFail(trait);
            if (!skill.NullOrEmpty()) skillDef = DefDatabase<SkillDef>.GetNamedSilentFail(skill);
            if (!meme.NullOrEmpty()) memeDef = DefDatabase<MemeDef>.GetNamedSilentFail(meme);
            if (!precept.NullOrEmpty()) preceptDef = DefDatabase<PreceptDef>.GetNamedSilentFail(precept);
        }

        /// <summary>Whether it fits this person (false when its trait, skill, meme or precept isn't in the game).</summary>
        public bool Fits(Pawn p)
        {
            Look();
            if (p == null) return false;
            if (!trait.NullOrEmpty())
            {
                var t = traitDef != null ? p.story?.traits?.GetTrait(traitDef) : null;
                if (t == null || t.Degree != degree) return false;
            }
            if (!skill.NullOrEmpty())
            {
                var s = skillDef != null ? p.skills?.GetSkill(skillDef) : null;
                if (s == null || s.TotallyDisabled || !People.Passionate(s)) return false;
            }
            if (!meme.NullOrEmpty())
            {
                if (memeDef == null || !ModsConfig.IdeologyActive || p.Ideo == null || !p.Ideo.HasMeme(memeDef)) return false;
            }
            if (!precept.NullOrEmpty())
            {
                if (preceptDef == null || !ModsConfig.IdeologyActive || p.Ideo == null || !p.Ideo.HasPrecept(preceptDef)) return false;
            }
            return true;
        }
    }

    /// <summary>Who or what a talk is about: a newcomer, the dead, the leader, the raiders' people. {SUBJECT} in the lines.</summary>
    public class TalkSubject
    {
        public Pawn pawn;
        public string name;
        /// <summary>The person's thingIDNumber, for a view about them (-1 for a thing or a people).</summary>
        public int id = -1;

        public static TalkSubject Of(Pawn p) => p == null ? null : new TalkSubject { pawn = p, name = p.LabelShort, id = p.thingIDNumber };

        public static TalkSubject Named(string name) => name.NullOrEmpty() ? null : new TalkSubject { name = name };
    }

    /// <summary>
    /// What a topic needs to know about the colony and its people. Each topic's worker answers: can it come up between
    /// these two now and how much (<see cref="Relevance"/>), is it news (<see cref="Hot"/>), who or what is it about
    /// (<see cref="Subject"/>), what someone's life now adds to their view (<see cref="Experience"/>), and any tags its
    /// lines are written for (<see cref="AddTags"/>). Asked when two colonists might talk, so it stays cheap: the scene
    /// (<see cref="TalkScene"/>) holds what is looked up once an hour.
    /// </summary>
    public class TopicWorker
    {
        public TopicDef def;

        /// <summary>Whether it can come up between these two now, and how much (0: not now).</summary>
        public virtual float Relevance(Pawn a, Pawn b, TalkScene s) => def.relevance;

        /// <summary>How hot it is now (0..1): something about it just happened.</summary>
        public virtual float Hot(TalkScene s) => TalkTracker.Instance?.HotFor(def) ?? 0f;

        /// <summary>Who or what it's about, for these two (null: nothing in particular).</summary>
        public virtual TalkSubject Subject(Pawn a, Pawn b, TalkScene s) => null;

        /// <summary>What someone's life now adds to their view (-0.6..0.6): a vendetta, their trust in the council, a meal.</summary>
        public virtual float Experience(Pawn p, TalkSubject subject, TalkScene s) => 0f;

        /// <summary>Tags the topic's lines can be written for (won, lost, mechs; sameideo; lowfood...).</summary>
        public virtual void AddTags(Pawn speaker, Pawn other, TalkScene s, TalkSubject subject, HashSet<string> tags)
        {
        }
    }

    /// <summary>Lines for one turn of a talk (open, reply, close, last), for a topic or memory (or any), and for some outcomes (or any).</summary>
    public class TalkLinesDef : Def
    {
        /// <summary>The topic these are about; null for lines that fit any topic.</summary>
        public TopicDef topic;
        /// <summary>The kind of shared memory these bring up (reminiscing, in-jokes); null for topic lines.</summary>
        public SharedMemoryDef memory;
        public TalkTurn turn;
        /// <summary>The outcomes these are for; empty for any.</summary>
        public List<TalkOutcome> outcomes = new List<TalkOutcome>();
        public ToneLines lines = new ToneLines();

        public bool For(TalkOutcome o) => outcomes.NullOrEmpty() || outcomes.Contains(o);

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            if (lines == null || !lines.Any) yield return "no lines";
            if (topic != null && memory != null) yield return "lines are for a topic or a memory, not both";
        }
    }

    /// <summary>What kind of talk an interaction starts.</summary>
    public enum TalkKind
    {
        /// <summary>A topic (the def's TopicDef points at the interaction).</summary>
        Topic,
        /// <summary>Remembering something they went through together.</summary>
        Memory,
        /// <summary>An in-joke of theirs.</summary>
        Joke,
        /// <summary>Comforting someone low or grieving.</summary>
        Comfort
    }

    /// <summary>On the talk interactions: what kind of talk, and its weight (another mod's own conversations can lower it, ModOverlapDef).</summary>
    public class TalkExtension : DefModExtension
    {
        public TalkKind kind = TalkKind.Topic;
        public float weight = 1f;
    }
}
