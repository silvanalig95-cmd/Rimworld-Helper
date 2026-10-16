using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A world situation ("world event" in the interface): something rare and region-wide that lasts a while (a plague,
    /// a famine, a warlord's rise, a neighbor's succession) and changes what the colony meets while it lasts. Rimfolk's
    /// colony-level events ("Rimfolk events") are drawn by the storyteller; these come on Rimfolk's own slow rhythm (see
    /// <see cref="WorldSituations"/>), at most two at a time and never two of one kind.
    ///
    /// <para><b>How one plays out.</b> When it starts, the worker picks its people (<see cref="faction"/>, <see cref="second"/>,
    /// a colonist), names it (<see cref="names"/>), applies <see cref="effects"/> and sends the start letter
    /// (<see cref="titles"/>, <see cref="texts"/>, then <see cref="summaries"/> as "what it means for the colony"). While it
    /// lasts, <see cref="modifiers"/> make some events likelier or rarer, and <see cref="beats"/> come at their days:
    /// letters, choices, arrivals, fights. Answers set flags and count as the colony's part (help). When time runs out,
    /// the first of the <see cref="endings"/> that fits is told; code can end it sooner with an ending of its own.</para>
    ///
    /// <para><b>Text.</b> Every list of lines is picked from by tags, like the rest of Rimfolk: era
    /// ([tribal], [medieval], [industrial], [spacer], [lowtech], [!lowtech]), season ([spring], [summer], [fall],
    /// [winter]), every flag set so far ([truce], [bounty]...), [helped] or [ignored] (whether the colony has taken
    /// part), [remembered] (the colony helped this people in an earlier world event) and whatever the worker adds.
    /// Every sentence gets a capital once the placeholders are filled in, so a name that comes in lowercase ({NAME},
    /// {TITLE}) may open one. Placeholders: {FACTION} (the people it's about), {ENEMY} (the second people), {PLACE} (a town, usually
    /// {FACTION}'s nearest), {NAME} (the situation's name: a warband, a fever, a fair), {TITLE} ({FACTION}'s word for
    /// its leader), {X_...} (its main person: the warlord, a new leader, a healer), {OTHER_...} (a second one), {A_...}
    /// and {B_...} (colonists), {COUNT}, {THING}, {ITEM}, {DAYS}, {TALE} and {DEAD} (what the worker fills in).</para>
    ///
    /// <para><b>Adding one.</b> Most need no code: a def in Defs/WorldSituationDefs/ with texts, modifiers, beats and
    /// endings, using <see cref="DilemmaEffect"/>s (the dilemma and event effects, plus the WorldEffect_* ones in
    /// SituationEffects.cs). Give it a <see cref="workerClass"/> only for what data can't say (the warlord's hunt, a
    /// settlement founded). Keep fights behind <see cref="SituationBeat.needsBattles"/>, never hurt colonists for flavor,
    /// and never turn a people hostile unless the colony chose it (WorldEffect_Goodwill stops short of that).</para>
    /// </summary>
    public class WorldSituationDef : Def
    {
        public Type workerClass = typeof(WorldSituationWorker);

        // ---------------- When it can happen ----------------

        public float weight = 1f;
        /// <summary>Not before this day of the game.</summary>
        public int earliestDay = 20;
        /// <summary>Days after it ends before it can come again.</summary>
        public float cooldownDays = 120f;
        public FloatRange durationDays = new FloatRange(10f, 20f);
        /// <summary>Only when it's one of these seasons at home (empty: any).</summary>
        public List<Season> seasons;
        /// <summary>Only for a colony at least this advanced (its effective tech level).</summary>
        public TechLevel minTech = TechLevel.Undefined;
        public TechLevel maxTech = TechLevel.Archotech;
        /// <summary>Only when one of these mods or DLCs is active (package ids).</summary>
        public List<string> mods;
        /// <summary>Never when one of these is active (a mod that already does something like it).</summary>
        public List<string> notWithMods;
        public int minColonists = 1;
        /// <summary>Only for a colony known this way (Merciful, Generous, Open...).</summary>
        public List<string> colonyIs;
        public List<string> colonyIsNot;
        /// <summary>Two situations of the same kind never run together (a famine and a locust year are both "hunger").</summary>
        public string kind;
        /// <summary>Brings fights of its own: only with world-event battles on and a storyteller that sends threats.</summary>
        public bool battle;
        /// <summary>
        /// A follow-up: only after one of these world events ended (defNames), between <see cref="afterDays"/> ago, and
        /// only once for each. Its people are that one's people when they fit its <see cref="faction"/> need; {TALE} names
        /// what came before, and its lines can ask for [after:defName] and [afterEnd:key].
        /// </summary>
        public List<string> after;
        public FloatRange afterDays = new FloatRange(1f, 60f);
        /// <summary>Only after it ended one of these ways (ending keys); empty: any way but quietly.</summary>
        public List<string> afterEndings;

        // ---------------- Who it's about ----------------

        /// <summary>The people it's about ({FACTION}). Null: none.</summary>
        public SituationFactionNeed faction;
        /// <summary>A second people ({ENEMY}): the warlord's hunters, the other side of a quarrel.</summary>
        public SituationFactionNeed second;
        /// <summary>A colonist it touches from the start ({A}). Null: none needed.</summary>
        public DilemmaRole colonist;

        // ---------------- What it says ----------------

        /// <summary>The start letter's title.</summary>
        [MustTranslate] public List<string> titles = new List<string>();
        /// <summary>The start letter: two or three short paragraphs.</summary>
        [MustTranslate] public List<string> texts = new List<string>();
        /// <summary>What it means for the colony while it lasts: under the start letter and on the World page.</summary>
        [MustTranslate] public List<string> summaries = new List<string>();
        /// <summary>{NAME}: the warband, the fever, the fair. Tagged by era like any line.</summary>
        [MustTranslate] public List<string> names;
        /// <summary>The start letter's kind (default: Rimfolk's world news letter).</summary>
        public LetterDef letter;
        /// <summary>Bad news: the start letter is the darker world news letter.</summary>
        public bool bad;

        // ---------------- What it does ----------------

        /// <summary>Applied when it starts (goodwill, thoughts, arrivals).</summary>
        public List<DilemmaEffect> effects = new List<DilemmaEffect>();
        /// <summary>Events made likelier or rarer while it lasts.</summary>
        public List<SituationModifier> modifiers = new List<SituationModifier>();
        /// <summary>What happens along the way: letters, questions, arrivals.</summary>
        public List<SituationBeat> beats = new List<SituationBeat>();
        /// <summary>How it can end. On timeout, the first that fits (in order) is told.</summary>
        public List<SituationEnding> endings = new List<SituationEnding>();

        [Unsaved] private WorldSituationWorker worker;

        public WorldSituationWorker Worker
        {
            get
            {
                if (worker == null)
                {
                    worker = (WorldSituationWorker)Activator.CreateInstance(workerClass ?? typeof(WorldSituationWorker));
                    worker.def = this;
                }
                return worker;
            }
        }

        public SituationBeat Beat(string key) => key.NullOrEmpty() ? null : beats.FirstOrDefault(b => b.key == key);

        public SituationEnding Ending(string key) => key.NullOrEmpty() ? null : endings.FirstOrDefault(e => e.key == key);

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            if (texts.NullOrEmpty()) yield return "world situation has no texts";
            if (endings.NullOrEmpty()) yield return "world situation has no endings";
            else if (!endings.Any(e => e.timeout)) yield return "no ending can be told when time runs out";
            if (workerClass != null && !typeof(WorldSituationWorker).IsAssignableFrom(workerClass)) yield return "workerClass is not a WorldSituationWorker";
            var keys = new HashSet<string>();
            foreach (var b in beats)
            {
                if (b.key.NullOrEmpty()) yield return "a beat has no key";
                else if (!keys.Add(b.key)) yield return "duplicate beat key " + b.key;
                if (!b.options.NullOrEmpty() && (b.defaultOption < 0 || b.defaultOption >= b.options.Count)) yield return "beat " + b.key + ": defaultOption out of range";
                foreach (var o in b.options ?? new List<SituationOption>())
                {
                    if (!o.next.NullOrEmpty() && Beat(o.next) == null) yield return "beat " + b.key + ": unknown next beat " + o.next;
                    if (!o.riskNext.NullOrEmpty() && Beat(o.riskNext) == null) yield return "beat " + b.key + ": unknown risk beat " + o.riskNext;
                    if (!o.end.NullOrEmpty() && Ending(o.end) == null) yield return "beat " + b.key + ": unknown ending " + o.end;
                }
            }
            foreach (var e in endings)
                if (e.key.NullOrEmpty()) yield return "an ending has no key";
        }
    }

    /// <summary>Which people a world situation can be about. All given conditions must hold.</summary>
    public class SituationFactionNeed
    {
        /// <summary>At war with the colony.</summary>
        public bool hostile;
        /// <summary>Not at war with the colony.</summary>
        public bool friendly;
        public int minGoodwill = -100;
        public int maxGoodwill = 100;
        /// <summary>Has a town on the colony's part of the world.</summary>
        public bool settlement = true;
        /// <summary>Has at least this many towns.</summary>
        public int minSettlements = 1;
        /// <summary>Its nearest town is at most this far (in world tiles).</summary>
        public float maxDistance = 9999f;
        /// <summary>Can send fighters.</summary>
        public bool canRaid;
        /// <summary>Has a leader the situation can replace (not an empire's throne).</summary>
        public bool leader;
        /// <summary>Has a creed of its own (Ideology).</summary>
        public bool ideo;
        /// <summary>The second people only: at war with the first.</summary>
        public bool hostileToFirst;
        /// <summary>The second people only: not at war with the first.</summary>
        public bool friendlyToFirst;
        /// <summary>It can happen without one (the beats and lines that need them check for it).</summary>
        public bool optional;
    }

    /// <summary>
    /// A storyteller weight changed while the situation lasts. Exactly one of <see cref="group"/> (a group of Rimfolk
    /// events: Past, Challenge, Battle, Nature, Visitors), <see cref="rimfolk"/> (a Rimfolk event's defName) or
    /// <see cref="incident"/> (one of the game's own incidents, by defName; only where the game picks by weight, and
    /// only with the deeper game hooks on). Names that don't exist in this game are simply ignored.
    /// </summary>
    public class SituationModifier
    {
        public string group;
        public string rimfolk;
        public string incident;
        public float factor = 1f;
        /// <summary>Only while this flag is set.</summary>
        public string whileFlag;
        /// <summary>Not once this flag is set.</summary>
        public string unlessFlag;

        [Unsaved] private bool parsed;
        [Unsaved] private HappeningGroup? groupValue;

        public HappeningGroup? Group
        {
            get
            {
                if (!parsed)
                {
                    parsed = true;
                    if (!group.NullOrEmpty() && Enum.TryParse(group, true, out HappeningGroup g)) groupValue = g;
                }
                return groupValue;
            }
        }

        public bool Applies(WorldSituation s) =>
            (whileFlag.NullOrEmpty() || s.Has(whileFlag)) && (unlessFlag.NullOrEmpty() || !s.Has(unlessFlag));

        public bool Matches(IncidentDef def, HappeningExtension ext)
        {
            if (def == null) return false;
            if (!incident.NullOrEmpty()) return ext == null && def.defName == incident;
            if (!rimfolk.NullOrEmpty()) return ext != null && def.defName == rimfolk;
            return ext != null && Group.HasValue && ext.group == Group.Value;
        }
    }

    /// <summary>
    /// Something that happens while a situation lasts: a letter, a question for the colony, an arrival, a fight. Beats
    /// come at their <see cref="day"/> (days after the start), or only when an answer or the worker calls them
    /// (<see cref="called"/>). With options it's a choice letter; without, a letter (or just a message).
    /// </summary>
    public class SituationBeat
    {
        public string key;
        /// <summary>When, in days after the start.</summary>
        public FloatRange day = new FloatRange(2f, 5f);
        /// <summary>Not scheduled at the start: only when an answer (next) or the worker calls it.</summary>
        public bool called;
        public float chance = 1f;
        /// <summary>Only when all these flags are set / none of these is.</summary>
        public List<string> requires;
        public List<string> forbids;
        /// <summary>Comes back every so often while the situation lasts, up to <see cref="maxTimes"/>.</summary>
        public FloatRange repeatDays = FloatRange.Zero;
        public int maxTimes = 1;
        /// <summary>Brings a fight: only with world-event battles on, and not in the middle of one.</summary>
        public bool needsBattles;
        /// <summary>Who it's put to ({A}): picked when it comes. Null: whoever the situation picked.</summary>
        public DilemmaRole colonist;
        /// <summary>With <see cref="colonist"/>: whoever is best at this skill, and at least <see cref="minLevel"/>.</summary>
        public SkillDef skill;
        public int minLevel;
        /// <summary>{COUNT}, rounded: silver, food or goods asked for, scaled by the colony's wealth when <see cref="wealthScaled"/>.</summary>
        public IntRange count = IntRange.Zero;
        public bool wealthScaled;
        [MustTranslate] public List<string> titles;
        [MustTranslate] public List<string> texts;
        /// <summary>Applied when it comes (before any answer).</summary>
        public List<DilemmaEffect> effects;
        /// <summary>A plain beat's letter kind (default: world news). With <see cref="message"/>: a message instead.</summary>
        public LetterDef letter;
        public bool message;
        public List<SituationOption> options;
        public int defaultOption;
        public float answerDays = 1.5f;
        /// <summary>A red choice letter.</summary>
        public bool threat;
    }

    /// <summary>One answer to a beat's question.</summary>
    public class SituationOption
    {
        [MustTranslate] public string label;
        /// <summary>What happened, as a message.</summary>
        [MustTranslate] public string outcome;
        public List<DilemmaEffect> effects = new List<DilemmaEffect>();
        /// <summary>A gamble: with this chance the risk happens too (or instead, with <see cref="riskReplaces"/>).</summary>
        public float riskChance;
        [MustTranslate] public string riskOutcome;
        public List<DilemmaEffect> riskEffects;
        public bool riskReplaces;
        /// <summary>A's skill at this lowers the risk.</summary>
        public SkillDef riskSkill;
        public WayShift ways;
        /// <summary>Flags set by this answer (endings, beats and modifiers read them).</summary>
        public List<string> flags;
        /// <summary>Flags set when the risk happens.</summary>
        public List<string> riskFlags;
        /// <summary>Flags cleared by this answer.</summary>
        public List<string> clears;
        /// <summary>How much this counts as the colony taking part.</summary>
        public int help;
        /// <summary>The colony's part, for the ending and the chronicle ("sent medicine to {FACTION}").</summary>
        [MustTranslate] public string deed;
        /// <summary>A beat this answer brings on, after <see cref="nextDays"/>.</summary>
        public string next;
        public FloatRange nextDays = new FloatRange(1f, 2f);
        /// <summary>A beat the risk brings on instead.</summary>
        public string riskNext;
        /// <summary>Ends the situation with this ending, after <see cref="endDays"/>.</summary>
        public string end;
        public FloatRange endDays = new FloatRange(1f, 2f);
        /// <summary>Not offered at all unless all these flags are set (or while any of <see cref="hideIf"/> is).</summary>
        public List<string> showIf;
        public List<string> hideIf;
    }

    /// <summary>How a situation can end.</summary>
    public class SituationEnding
    {
        public string key;
        /// <summary>Can be picked when time runs out (code-only endings: false).</summary>
        public bool timeout = true;
        public List<string> requires;
        public List<string> forbids;
        /// <summary>Only when the colony's part is at least / at most this.</summary>
        public int minHelp = -999;
        public int maxHelp = 999;
        /// <summary>On timeout, this one is passed over this often even when it fits.</summary>
        public float chance = 1f;
        [MustTranslate] public List<string> titles;
        [MustTranslate] public List<string> texts;
        /// <summary>One line for the chronicle (default: the start of the letter).</summary>
        [MustTranslate] public string chronicle;
        public LetterDef letter;
        public List<DilemmaEffect> effects;
        /// <summary>Counts as the colony having helped {FACTION} (later world events remember it).</summary>
        public bool helped;
    }
}
