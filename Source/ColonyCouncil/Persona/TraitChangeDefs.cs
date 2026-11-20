using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Marks a person must carry for a trait to change: any of these (mark defNames, families or stories), at least
    /// <see cref="count"/> of them. Only marks since this trait last changed count, so one stretch of life earns one change.
    /// </summary>
    public class TraitMarkNeed
    {
        /// <summary>
        /// "Rimfolk_Mark_HeldTheLine, ManyRaids, Loss": a mark's defName, a family (every mark of it) or a story (every
        /// mark that tells it: Loss is any death that marked them, Betrayal a secret given away either way).
        /// </summary>
        public string any;
        public int count = 1;
        /// <summary>Only marks from the last this many days (0: any they still carry).</summary>
        public int withinDays;

        [Unsaved] private HashSet<string> names;

        public HashSet<string> Names
        {
            get
            {
                if (names == null)
                {
                    names = new HashSet<string>();
                    if (!any.NullOrEmpty())
                        foreach (var s in any.Split(','))
                            if (s.Trim().Length > 0) names.Add(s.Trim());
                }
                return names;
            }
        }

        public bool Matches(PersonaMarkDef def) => TraitChangeDef.Named(Names, def);
    }

    /// <summary>
    /// Things the person did that the game tells only in passing (a memory, a deed in the game's history, a tale, a
    /// breakdown), or marks they were given, counted on separate occasions and kept under <see cref="key"/> (rules that name the same key
    /// share the count). Kept 240 days whatever happens to the mark itself, so "two long peaces" can be counted although
    /// a long peace leaves the character page before the next one comes.
    /// </summary>
    public class TraitTally
    {
        public string key;
        /// <summary>Memories they gained (ThoughtDef names; one from a DLC or mod that isn't loaded is skipped).</summary>
        public string thoughts;
        /// <summary>Deeds in the game's history (HistoryEventDef names) with them as the doer.</summary>
        public string events;
        /// <summary>Tales the game tells (TaleDef names) with them as the first person in it.</summary>
        public string tales;
        /// <summary>Marks they were given (defNames, families or stories, as a rule's marks name them), counted when they come.</summary>
        public string marks;
        /// <summary>Breakdowns they had from their mood (MentalStateDef names: "Binging_Food"), counted when they start.</summary>
        public string mentalStates;
        /// <summary>Only while they were starving or malnourished, or in a hungry season.</summary>
        public bool whileHungry;
        /// <summary>Only while they were drunk (the game's alcohol high on them): held the line drunk, for the drunken master.</summary>
        public bool whileDrunk;
        public int count = 1;
        /// <summary>Only from the last this many days (0: all kept, which is <see cref="TraitChanges.TallyKeepDays"/>).</summary>
        public int withinDays;
        /// <summary>
        /// A zero count: none of them for this many days, counted from the newest one or from the trait's last change,
        /// whichever is later, and never from before they joined the colony (<see cref="TraitChangeMath.NoneDays"/>); a
        /// zero count of marks also reads the marks still on their character page. With it, <see cref="count"/> and
        /// <see cref="withinDays"/> don't apply. At most <see cref="TraitChanges.TallyKeepDays"/>.
        /// </summary>
        public int noneDays;
        /// <summary>
        /// With <see cref="noneDays"/>: the zero count starts only once the rule's tally with this key has counted one
        /// ("a hungry season, then 60 days without a food binge"; <see cref="TraitChangeMath.NoneDaysAfter"/>).
        /// </summary>
        public string noneAfter;
        /// <summary>What one of them is called in the letter ("ate human flesh while starving").</summary>
        [MustTranslate] public string label;

        public bool None => noneDays > 0;

        /// <summary>It asks how they were at the time (hungry, drunk), which the marks still on their page can't tell.</summary>
        public bool Filtered => whileHungry || whileDrunk;

        public IEnumerable<string> Split(string list) =>
            list.NullOrEmpty() ? Enumerable.Empty<string>() : list.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0);

        [Unsaved] private HashSet<string> markNames;

        /// <summary>Whether a mark given counts here: named by its defName, its family or its story.</summary>
        public bool CountsMark(PersonaMarkDef def)
        {
            markNames ??= new HashSet<string>(Split(marks));
            return TraitChangeDef.Named(markNames, def);
        }
    }

    /// <summary>
    /// A share of their time over a stretch, from the game's own records: time at recreation (TimeGettingJoy) over time
    /// in the colony (TimeAsColonistOrColonyAnimal), say. At least or at most a share, over <see cref="days"/> days.
    /// </summary>
    public class TraitPace
    {
        public string record;
        public string per = "TimeAsColonistOrColonyAnimal";
        public float atLeast = -1f;
        public float atMost = -1f;
        public int days = 60;
        /// <summary>What it measures, in the letter: "time at leisure".</summary>
        [MustTranslate] public string label;

        public bool Holds(float share) => share >= 0f && (atLeast < 0f || share >= atLeast) && (atMost < 0f || share <= atMost);
    }

    /// <summary>
    /// A rule for one of the game's traits growing, appearing or fading with what happens to a colonist
    /// (Persona/TraitChanges.cs). Everything it asks for is something the player can read: marks on the character
    /// page, a side moved from their nature, deeds counted, records the game keeps, a vow of vengeance. When it holds, the
    /// chance is rolled once a day; the rate limits keep it rare (one change per person in 60 days, one in the colony in
    /// 10; taking away a trait Rimfolk gave isn't held back by the person's 60, and a way out of a bad trait isn't held
    /// back by the colony's 10). Where a rule would read "this or that" (a seat, or two promises kept), it is two rules.
    /// </summary>
    public class TraitChangeDef : Def
    {
        /// <summary>The trait's defName (from the game, a DLC or a mod; missing ones make the rule do nothing).</summary>
        public string trait;
        public TraitChangeKind change = TraitChangeKind.Gain;
        /// <summary>Gain: the degree gained. Lose: the degree that goes (or <see cref="anyDegree"/>).</summary>
        public int degree;
        public bool anyDegree;
        /// <summary>A step: the degrees it steps from (none is 0); empty: any it can.</summary>
        public List<int> from = new List<int>();

        /// <summary>How far life has moved them from their nature (lasting and fading): "nerve&gt;=15".</summary>
        public string moved;
        /// <summary>Where they are now: "warmth&gt;=40".</summary>
        public string now;
        /// <summary>Marks they must carry; every entry must hold.</summary>
        public List<TraitMarkNeed> marks = new List<TraitMarkNeed>();
        /// <summary>None of these marks (defNames or families) in the last <see cref="notMarksDays"/> days.</summary>
        public string notMarks;
        public int notMarksDays = 60;
        /// <summary>Deeds counted on separate occasions; every entry must hold.</summary>
        public List<TraitTally> tallies = new List<TraitTally>();
        /// <summary>The game's records of them (RecordDef names): "KillsHumanlikes&gt;=30".</summary>
        public string records;
        /// <summary>Their skills: "Melee&gt;=10".</summary>
        public string skills;
        /// <summary>Shares of their time over a stretch; every entry must hold.</summary>
        public List<TraitPace> pace = new List<TraitPace>();
        /// <summary>Days without a breakdown or any other mental state (0: no condition).</summary>
        public int calmDays;
        /// <summary>The calm days count only since this trait last changed (a calm from before doesn't count).</summary>
        public bool calmSinceChange;
        /// <summary>
        /// Their vows of vengeance (Secrets/Vendettas.cs): "unpaid", one standing against a people still at war with the
        /// colony for <see cref="vendettaDays"/> days or more with none of them fallen to the avenger yet, for a loss that
        /// hasn't already made someone take this trait; "over", none left standing (avenged, laid down, lapsed, the one
        /// taken home, or that people at peace with the colony). With vendettas switched off no vow stands.
        /// </summary>
        public string vendetta;
        public int vendettaDays = 30;
        /// <summary>Never for someone with a rival in the colony: a rivalry on the Bonds page, or the game's opinion of -20 or less.</summary>
        public bool noRival;
        /// <summary>Only takes away a trait Rimfolk gave them (the newest change of it kept here gave it).</summary>
        public bool onlyGiven;
        /// <summary>
        /// With <see cref="onlyGiven"/>: only when one of these rules (defNames) gave it, so what famine brought goes with
        /// the famine and what a faith brought with the faith. Empty: whichever rule gave it.
        /// </summary>
        public List<string> givenBy;
        /// <summary>Only for a trait Rimfolk didn't give them (one they came with): the slower twin of a rule with <see cref="onlyGiven"/>.</summary>
        public bool notGiven;
        /// <summary>A friend on the Bonds page: a friendship announced between them and someone living.</summary>
        public bool friend;
        /// <summary>An animal bonded to them, alive.</summary>
        public bool bondedAnimal;
        /// <summary>
        /// With <see cref="bondedAnimal"/>: one of the colony's dogs ("dog") or cats ("cat"), by
        /// <see cref="TraitChangeMath.PetKind"/>; empty, any animal.
        /// </summary>
        public string bondedKind;
        /// <summary>With <see cref="bondedAnimal"/>: bonded at least this many days (the bond's own start; 60 is a year).</summary>
        public int bondedDays;
        /// <summary>They have one of these traits (defNames): a brawler, for the drunken master.</summary>
        public List<string> withTrait;
        /// <summary>
        /// The degrees this rule leads out of (0 for a trait without degrees): it is a way out of a bad trait when the person
        /// has the trait at one of them (<see cref="TraitChangeMath.IsWayOut"/>). A way out isn't held back by the colony's
        /// 10 days, and the character page shows it from the start, with what is still missing.
        /// </summary>
        public List<int> wayOutOf;
        /// <summary>Their ideoligion holds none of these precepts (PreceptDef names); without an ideoligion it holds.</summary>
        public string notPrecepts;
        /// <summary>What that means, for the player: "a faith that does not call for eating human flesh".</summary>
        [MustTranslate] public string notPreceptsLabel;
        /// <summary>Days in the colony (0: no condition).</summary>
        public int daysHere;
        public int minAge;
        /// <summary>A melee fighter: their Melee two above their Shooting, and no ranged weapon in hand.</summary>
        public bool meleeFighter;
        /// <summary>
        /// A habit they hold (a QuirkDef's defName): had at least <see cref="habitDays"/> days, and lived on at least
        /// <see cref="habitLived"/> of them (at its work, or its moment in their free time). A habit that grows into the
        /// trait ends with the change (<see cref="endsHabit"/>).
        /// </summary>
        public string habit;
        public int habitDays = 90;
        public int habitLived = 60;
        public bool endsHabit = true;
        /// <summary>What they've been doing lately (Quirks/Practice.cs): "night&gt;=40, Hauling&gt;=60" (recent hours).</summary>
        public string practice;
        /// <summary>
        /// Traits (defNames) that do this one's job when their mod is loaded: then this rule is off, and another names
        /// that trait (Rimfolk's own traits give way to Vanilla Traits Expanded's).
        /// </summary>
        public List<string> insteadOf;

        /// <summary>Days since this person's last change of the same trait.</summary>
        public int gapDays = 120;
        /// <summary>The chance per daily check once everything holds.</summary>
        public float chance = 0.1f;
        /// <summary>Traits that keep it from happening (besides the game's own conflicts).</summary>
        public List<string> blockedBy = new List<string>();
        /// <summary>Rules with the same key happen at most once in a colony's life, all together.</summary>
        public string onceKey;
        /// <summary>Only with this DLC: Royalty, Ideology, Biotech, Anomaly or Odyssey.</summary>
        public string dlc;
        /// <summary>Only with one of these mods (package ids).</summary>
        public List<string> mods;
        /// <summary>Only from this tech level on (the colony's, by what it has researched).</summary>
        public TechLevel minTech = TechLevel.Undefined;

        /// <summary>1 good news (a positive letter), -1 bad (a negative one), 0 neither.</summary>
        public int tone;
        /// <summary>The news: "{PAWN_nameDef} has become steadfast." Starts the letter and goes into the chronicle.</summary>
        [MustTranslate] public string news;
        /// <summary>What it means, a sentence after the news: what changed in them, in plain words.</summary>
        [MustTranslate] public string meaning;
        /// <summary>A step that ends at none (nervous steadied, an optimist worn down): the news then, or a plain "is no longer".</summary>
        [MustTranslate] public string newsLost;
        /// <summary>What they write in their journal: {PAWN}, {TRAIT} (the trait's label after the change, or before for a loss).</summary>
        public ToneLines journal = new ToneLines();

        [Unsaved] private List<MarkCond> movedConds, nowConds;
        [Unsaved] private List<TraitFact> recordFacts, skillFacts, practiceFacts;
        [Unsaved] private QuirkDef habitDef;
        [Unsaved] private bool habitLooked;
        [Unsaved] private string parseError;
        [Unsaved] private bool parsed;
        [Unsaved] private TraitDef traitDef;
        [Unsaved] private bool looked;
        [Unsaved] private HashSet<string> notMarkNames;

        private void Parse()
        {
            if (parsed) return;
            parsed = true;
            var errors = new List<string>();
            if (!MarksMath.TryParseWhen(moved, out movedConds, out string e1)) errors.Add("moved: " + e1);
            if (!MarksMath.TryParseWhen(now, out nowConds, out string e2)) errors.Add("now: " + e2);
            if (!TraitChangeMath.TryParseFacts(records, out recordFacts, out string e3)) errors.Add("records: " + e3);
            if (!TraitChangeMath.TryParseFacts(skills, out skillFacts, out string e4)) errors.Add("skills: " + e4);
            if (!TraitChangeMath.TryParseFacts(practice, out practiceFacts, out string e5)) errors.Add("practice: " + e5);
            practiceFacts ??= new List<TraitFact>();
            movedConds ??= new List<MarkCond>();
            nowConds ??= new List<MarkCond>();
            recordFacts ??= new List<TraitFact>();
            skillFacts ??= new List<TraitFact>();
            notMarkNames = new HashSet<string>();
            if (!notMarks.NullOrEmpty())
                foreach (var s in notMarks.Split(','))
                    if (s.Trim().Length > 0) notMarkNames.Add(s.Trim());
            if (errors.Count > 0) parseError = string.Join("; ", errors);
        }

        public List<MarkCond> Moved
        {
            get
            {
                Parse();
                return movedConds;
            }
        }

        public List<MarkCond> Now
        {
            get
            {
                Parse();
                return nowConds;
            }
        }

        public List<TraitFact> Records
        {
            get
            {
                Parse();
                return recordFacts;
            }
        }

        public List<TraitFact> Skills
        {
            get
            {
                Parse();
                return skillFacts;
            }
        }

        public List<TraitFact> Practice
        {
            get
            {
                Parse();
                return practiceFacts;
            }
        }

        /// <summary>The habit it grows from, if any.</summary>
        public QuirkDef Habit
        {
            get
            {
                if (!habitLooked)
                {
                    habitLooked = true;
                    habitDef = habit.NullOrEmpty() ? null : DefDatabase<QuirkDef>.GetNamedSilentFail(habit);
                }
                return habitDef;
            }
        }

        public bool NotMark(PersonaMarkDef def)
        {
            Parse();
            return notMarkNames.Count > 0 && Named(notMarkNames, def);
        }

        /// <summary>A mark named among these by its defName, its family or its story.</summary>
        public static bool Named(HashSet<string> names, PersonaMarkDef def)
        {
            if (def == null || names == null || names.Count == 0) return false;
            if (names.Contains(def.defName) || names.Contains(MarkDefs.FamilyOf(def))) return true;
            string story = MarkDefs.StoryOf(def);
            return story != null && names.Contains(story);
        }

        /// <summary>The trait, if the game (or the mod it's from) has it.</summary>
        public TraitDef Trait
        {
            get
            {
                if (!looked)
                {
                    looked = true;
                    traitDef = trait.NullOrEmpty() ? null : DefDatabase<TraitDef>.GetNamedSilentFail(trait);
                }
                return traitDef;
            }
        }

        public bool IsStep => change == TraitChangeKind.StepUp || change == TraitChangeKind.StepDown;

        /// <summary>The rule can lead out of a bad trait (for some degree it steps from).</summary>
        public bool LeadsOut => !wayOutOf.NullOrEmpty();

        /// <summary>This change, for this person, leads out of a bad trait.</summary>
        public bool WayOut(TraitStep step) => TraitChangeMath.IsWayOut(wayOutOf, step);

        [Unsaved] private List<string> preceptNames;

        /// <summary>The precepts their ideoligion must not hold (names, as written).</summary>
        public List<string> NotPrecepts => preceptNames ??= notPrecepts.NullOrEmpty()
            ? new List<string>()
            : notPrecepts.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();

        public const string VendettaUnpaid = "unpaid", VendettaOver = "over";

        /// <summary>The DLC and mods it needs are there, and the colony is far enough along.</summary>
        public bool Available
        {
            get
            {
                if (Trait == null) return false;
                if (!Present) return false;
                if (!insteadOf.NullOrEmpty() && insteadOf.Any(n => DefDatabase<TraitDef>.GetNamedSilentFail(n) != null)) return false;
                if (RimfolkTraits.Doubled(Trait)) return false;
                if (!habit.NullOrEmpty() && Habit == null) return false;
                return minTech == TechLevel.Undefined || Avail.EffectiveTech >= minTech;
            }
        }

        /// <summary>
        /// The DLC and one of the mods it names are loaded (the game's own content it reads is there: the fishing work is
        /// Odyssey's, dark study Anomaly's).
        /// </summary>
        public bool Present
        {
            get
            {
                if (!mods.NullOrEmpty() && !mods.Any(People.ModActive)) return false;
                switch (dlc)
                {
                    case "Royalty": return ModsConfig.RoyaltyActive;
                    case "Ideology": return ModsConfig.IdeologyActive;
                    case "Biotech": return ModsConfig.BiotechActive;
                    case "Anomaly": return ModsConfig.AnomalyActive;
                    case "Odyssey": return ModsConfig.OdysseyActive;
                    default: return true;
                }
            }
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            Parse();
            if (parseError != null) yield return parseError;
            if (trait.NullOrEmpty()) yield return "no trait";
            string never = TraitChangeMath.Untouchable(trait);
            if (never != null && trait != null) yield return "trait " + trait + " is never changed (" + never + ")";
            if (chance <= 0f || chance > 1f) yield return "chance must be above 0 and at most 1";
            if (trait != null && !TraitChangeMath.TrainedOnlyGrows(trait, change == TraitChangeKind.Gain || change == TraitChangeKind.StepUp))
                yield return "trait " + trait + " is the body's, earned by its history: it is only gained or stepped up";
            if (news.NullOrEmpty()) yield return "no news";
            if (journal == null || !journal.Any) yield return "no journal lines";
            if (!dlc.NullOrEmpty() && !new[] { "Royalty", "Ideology", "Biotech", "Anomaly", "Odyssey" }.Contains(dlc)) yield return "unknown dlc " + dlc;
            bool evidence = (marks != null && marks.Count > 0) || (tallies != null && tallies.Count > 0) || (pace != null && pace.Count > 0) || calmDays > 0
                            || !records.NullOrEmpty() || !habit.NullOrEmpty() || !practice.NullOrEmpty() || !vendetta.NullOrEmpty() || friend || bondedAnimal;
            if (notGiven && onlyGiven) yield return "notGiven and onlyGiven can't both hold";
            if (!vendetta.NullOrEmpty() && vendetta != VendettaUnpaid && vendetta != VendettaOver) yield return "vendetta must be unpaid or over, not " + vendetta;
            if (vendetta == VendettaUnpaid && vendettaDays < 1) yield return "an unpaid vendetta needs its days";
            if (onlyGiven && change != TraitChangeKind.Lose) yield return "onlyGiven is for a loss: it undoes what Rimfolk gave";
            if (onlyGiven && !LeadsOut) yield return "onlyGiven takes away a bad trait Rimfolk gave: name it a way out (wayOutOf)";
            if (!givenBy.NullOrEmpty())
            {
                if (!onlyGiven) yield return "givenBy needs onlyGiven";
                foreach (var g in givenBy)
                {
                    var by = DefDatabase<TraitChangeDef>.GetNamedSilentFail(g);
                    if (by == null) yield return "givenBy names no rule " + g;
                    else if (by.trait != trait || by.change != TraitChangeKind.Gain) yield return "givenBy " + g + " doesn't give " + trait;
                }
            }
            if (LeadsOut)
            {
                if (change == TraitChangeKind.Gain) yield return "a gain is never a way out of a bad trait";
                if (tone <= 0) yield return "a way out of a bad trait is good news (tone 1)";
                if (IsStep && !from.NullOrEmpty())
                    foreach (int d in wayOutOf)
                        if (!from.Contains(d)) yield return "wayOutOf " + d + " is a degree it doesn't step from";
            }
            if (!notPrecepts.NullOrEmpty() && notPreceptsLabel.NullOrEmpty()) yield return "notPrecepts needs notPreceptsLabel";
            if (calmSinceChange && calmDays <= 0) yield return "calmSinceChange needs calmDays";
            if (!habit.NullOrEmpty() && Habit == null) yield return "unknown habit " + habit;
            // A kind of work from a DLC or a mod is only there with it (Odyssey's fishing, Anomaly's dark study).
            if (Present)
                foreach (var f in Practice)
                    foreach (var part in TraitChangeMath.PracticeParts(f.name))
                        if (!ColonyCouncil.Practice.IsKnown(part)) yield return "unknown practice " + part;
            if ((!bondedKind.NullOrEmpty() || bondedDays > 0) && !bondedAnimal) yield return "bondedKind and bondedDays need bondedAnimal";
            if (!bondedKind.NullOrEmpty() && bondedKind != TraitChangeMath.Dog && bondedKind != TraitChangeMath.Cat)
                yield return "bondedKind must be " + TraitChangeMath.Dog + " or " + TraitChangeMath.Cat + ", not " + bondedKind;
            if (!withTrait.NullOrEmpty() && Present)
                foreach (var n in withTrait)
                    if (DefDatabase<TraitDef>.GetNamedSilentFail(n) == null) yield return "withTrait names no trait " + n;
            if (!evidence) yield return "a change with nothing behind it: give it marks, deeds, records, a pace or calm days";
            if (marks != null)
                foreach (var m in marks)
                    if (m == null || m.Names.Count == 0 || m.count < 1) yield return "a marks entry without marks or count";
            if (tallies != null)
                foreach (var t in tallies)
                {
                    if (t == null || t.key.NullOrEmpty()) yield return "a tally without a key";
                    else if (t.thoughts.NullOrEmpty() && t.events.NullOrEmpty() && t.tales.NullOrEmpty() && t.marks.NullOrEmpty() && t.mentalStates.NullOrEmpty())
                        yield return "tally " + t.key + " counts nothing";
                    else if (t.label.NullOrEmpty()) yield return "tally " + t.key + " has no label";
                    else if (t.noneDays < 0 || t.noneDays > TraitChanges.TallyKeepDays) yield return "tally " + t.key + ": noneDays from 1 to " + TraitChanges.TallyKeepDays;
                    else if (t.whileDrunk && t.None) yield return "tally " + t.key + ": whileDrunk counts moments, not a stretch without them";
                    else if (tallies.Any(o => o != null && o != t && o.key == t.key && (o.whileHungry != t.whileHungry || o.whileDrunk != t.whileDrunk)))
                        yield return "tally " + t.key + ": the same key counted two ways (whileHungry, whileDrunk); give each its own key";
                    else if (!t.noneAfter.NullOrEmpty() && (!t.None || !tallies.Any(o => o != null && o != t && o.key == t.noneAfter && !o.None)))
                        yield return "tally " + t.key + ": noneAfter needs noneDays and names another tally of this rule that counts (" + t.noneAfter + ")";
                }
            if (pace != null)
                foreach (var p in pace)
                    if (p == null || p.record.NullOrEmpty() || (p.atLeast < 0f && p.atMost < 0f) || p.days < 10) yield return "a pace needs a record, a bound and 10 days or more";
        }

        public static List<TraitChangeDef> All => DefDatabase<TraitChangeDef>.AllDefsListForReading;
    }
}
