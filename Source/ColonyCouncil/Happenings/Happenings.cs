using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>What kind of world event it is (for variety, and for the settings).</summary>
    public enum HappeningGroup
    {
        /// <summary>A colonist's past catches up with them.</summary>
        Past,
        /// <summary>A challenge or a gamble the colony can take up or leave.</summary>
        Challenge,
        /// <summary>Something that may end in a fight.</summary>
        Battle,
        /// <summary>The land and the sky.</summary>
        Nature,
        /// <summary>Visitors and wanderers.</summary>
        Visitors
    }

    /// <summary>
    /// What a Rimfolk world event says and offers. World events are ordinary IncidentDefs in their own category
    /// (Council_WorldEvent) with this extension: Rimfolk schedules them on its own rhythm, so they never take a slot
    /// from the storyteller's own events, and dev mode can fire any of them. Texts may use {A}, {B}, {X} (the stranger),
    /// {FACTION}, {ENEMY}, {COUNT}, {THING}, {SKILL}, {RELATION}, {DEAD}, {BEAST}, {TALE} and {PLACE}. Lines can carry era tags
    /// ([lowtech], [!lowtech], [industrial|spacer]) and event tags (the colonist's past, the skill, the season).
    /// </summary>
    public class HappeningExtension : DefModExtension
    {
        public HappeningGroup group = HappeningGroup.Visitors;
        /// <summary>The visitor it brings is one the colony keeps up with afterwards (Happenings/Regulars.cs): their trade (tinker, scholar, doctor, healer, teacher, cook, scribe, teller, kindred); "back" for the return itself.</summary>
        public string regular;
        /// <summary>Colony trait tags (FamedHall, Artful, Hallowed, Walled, Hospitable, Learned...): the event comes two and a half times as often while the colony is known for one of them.</summary>
        public List<string> drawnBy;
        /// <summary>Brings a fight: only with battles switched on, threats allowed, and no fight going on or just past.</summary>
        public bool battle;
        /// <summary>Someone may join: rarer while the storyteller thinks the colony is big enough.</summary>
        public bool addsPeople;
        /// <summary>Only when one of these mods (or DLCs) is active, by package id.</summary>
        public List<string> mods;
        public List<string> colonyIs;
        public List<string> colonyIsNot;
        public List<Season> seasons;
        public int minColonists = 1;
        /// <summary>Who it happens to (A). Without one: anyone.</summary>
        public DilemmaRole colonist;
        /// <summary>A second colonist (B), when the event needs one.</summary>
        public DilemmaRole second;
        [MustTranslate] public List<string> titles = new List<string>();
        [MustTranslate] public List<string> texts = new List<string>();
        /// <summary>Applied when it happens, before any answer.</summary>
        public List<DilemmaEffect> effects = new List<DilemmaEffect>();
        /// <summary>A choice for the colony; none = just a letter.</summary>
        public List<DilemmaOption> options = new List<DilemmaOption>();
        public int defaultOption;
        public float answerDays = 1f;
        /// <summary>A's skill at this lowers the risk of a risky option (talking someone down, haggling).</summary>
        public SkillDef riskSkill;
        /// <summary>Which option the risk skill helps with (-1 = every risky one).</summary>
        public int riskSkillOption = -1;
        /// <summary>A red letter.</summary>
        public bool threat;
        /// <summary>The kind of letter for an event without a choice (positive, neutral, negative...).</summary>
        public LetterDef letter;
        /// <summary>A flavor pool for A's journal ({X_...} and the named arguments above work).</summary>
        public string journal;
        /// <summary>Goes in the chronicle.</summary>
        public bool notable = true;
        /// <summary>How it shapes the colony's character when it happens (choices carry their own).</summary>
        public WayShift ways;

        // ---- Knobs some events read ----
        public FloatRange ages = new FloatRange(18f, 50f);
        public PawnRelationDef relation;
        /// <summary>A fight's strength, as a share of what the storyteller would send now.</summary>
        public FloatRange points = new FloatRange(0.5f, 0.7f);
        public FloatRange delayHours = new FloatRange(3f, 8f);
        public FloatRange stayHours = new FloatRange(16f, 26f);
        /// <summary>A sum of silver ({COUNT}).</summary>
        public IntRange amount = new IntRange(0, 0);
        public IntRange count = new IntRange(1, 1);
        /// <summary>What a find or a gift is worth, in silver.</summary>
        public FloatRange value = new FloatRange(300f, 800f);
        public GameConditionDef condition;
        public FloatRange days = new FloatRange(2f, 4f);
        /// <summary>Only when it's this warm (or this cold) outside.</summary>
        public float minTemp = -999f;
        public float maxTemp = 999f;
        /// <summary>
        /// The weather of the last days, as the colony's weather record has it (Weather/WeatherBook.cs), for an event
        /// that speaks of it: "rain:5/7" (rain on 5 of the last 7 days), "snow:N/M", "frost:N/M" (below freezing all
        /// day), "dry:M" (nothing fell), "storm:M"; "[fall]rain:3/7" asks it only in fall. All must fit.
        /// </summary>
        public List<string> weather;
        /// <summary>
        /// What the event does to the weather that follows (Weather/WeatherDynamics.cs, with "Weather that changes like
        /// weather" on): "rain:1.8:5" makes rain likelier for five days, "clear:0.6:5" clear skies less likely; kinds are
        /// rain, storm, clear, snow and fog. The game still chooses each weather from the land's own.
        /// </summary>
        public List<string> weatherAfter;
        public ThoughtDef thought;
        public ThoughtDef thoughtOthers;
        /// <summary>Short lines spoken or shown above heads while it plays out (tales, cheers, names).</summary>
        [MustTranslate] public List<string> lines;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            if (texts.NullOrEmpty()) yield return "world event has no texts";
            if (!options.NullOrEmpty() && (defaultOption < 0 || defaultOption >= options.Count)) yield return "defaultOption out of range";
        }
    }

    /// <summary>The people and things of one world event as it happens.</summary>
    public class HappeningContext : DilemmaContext
    {
        public IncidentDef incident;
        /// <summary>While an answer is being given: what it set going on the map, so the event's own result can wait for it.</summary>
        [Unsaved] public List<PlayOut> plays;
        /// <summary>Whoever came along with X (pilgrims, mourners, a challenger's friends).</summary>
        public List<Pawn> group = new List<Pawn>();
        /// <summary>What X is to A ("sister").</summary>
        public string relation;
        /// <summary>A name from the past: the dead, the executed.</summary>
        public string dead;
        public string beast;
        /// <summary>A story told about the colony, or what a scholar studies.</summary>
        public string tale;
        /// <summary>An invitation's host by their title, its rite, and the colony's guest by theirs ({HOST}, {RITE}, {GUEST}: Missions/InviteForms.cs).</summary>
        public string host, rite, guest;
        public float points;
        /// <summary>Who would come to fight.</summary>
        public Faction enemy;
        /// <summary>A grave, a cache.</summary>
        public Thing place;
        /// <summary>Things the event brought (a lost cargo), to give back later.</summary>
        public List<Thing> things = new List<Thing>();
        /// <summary>Where it happened, when there's no thing to point at yet (a pod still falling).</summary>
        public IntVec3 spot = IntVec3.Invalid;
        /// <summary>X is a world pawn until the colony answers (joins at the edge of the map).</summary>
        public bool xWaiting;
        public int variant;
        /// <summary>{SKILL} names a contest ("marksmanship", "wrestling") rather than a craft.</summary>
        public bool contest;
        /// <summary>Set off by the scenario the player chose: its fights ignore the world-event battle switch.</summary>
        public bool scenario;
        /// <summary>A place the texts name ({PLACE}): a neighbor's town, the guest room, the land around the colony.</summary>
        public string placeName;
        /// <summary>Something the event's code keeps for the answer (a def name, where a stray stands), never shown.</summary>
        public string note;

        public HappeningExtension Ext => incident?.GetModExtension<HappeningExtension>();

        public IncidentWorker_Happening Worker => incident?.Worker as IncidentWorker_Happening;

        public override string SkillText => HappeningText.SkillNoun(skill, contest);

        /// <summary>
        /// What is true of the event's people for its lines (the facts service's event scene: what they remember of the
        /// colony, the ties, their town lately, the region's word on it), read once when its text is first picked.
        /// </summary>
        [Unsaved] private FactSet facts;
        [Unsaved] private bool factsRead;

        /// <summary>The people the event is about: its own, else the stranger's.</summary>
        public Faction People => faction ?? (stranger != null && stranger.RaceProps.Humanlike ? stranger.Faction : null);

        public FactSet Facts
        {
            get
            {
                if (factsRead) return facts;
                factsRead = true;
                try
                {
                    var people = People;
                    if (people == null || !FactionMemory.Remembers(people)) return facts = null;
                    var town = (stranger != null ? Outsiders.TownOf(stranger) : null) ?? WorldSituationWorker.NearestTown(people, map);
                    facts = ColonyCouncil.Facts.For(FactScene.Event(map, people, town, stranger));
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("an event's facts", e);
                    facts = null;
                }
                return facts;
            }
        }

        protected override void AddArgs(List<NamedArgument> args)
        {
            args.Add((relation ?? "").Named("RELATION"));
            args.Add((dead ?? "").Named("DEAD"));
            args.Add((beast ?? "").Named("BEAST"));
            args.Add((enemy?.Name ?? "").Named("ENEMY"));
            args.Add((tale ?? "").Named("TALE"));
            args.Add((placeName ?? "").Named("PLACE"));
            // An invitation's host by their title, the rite of the day, the colony's titled guest (Missions/InviteForms.cs).
            args.Add((host ?? "").Named("HOST"));
            args.Add((rite ?? "").Named("RITE"));
            args.Add((guest ?? "").Named("GUEST"));
            // The colonist their people remember by name (World/KinMemory.cs), for lines tagged [their:friend].
            args.Add(KinMemory.FriendArg(People));
            // What their people remember and the region's word on their town ({DEED}, {WRONG}, {FORTUNE}...), for the lines
            // tagged with the facts behind them; never one of the event's own names.
            var f = Facts;
            if (f == null) return;
            var taken = new HashSet<string>();
            foreach (var a in args) taken.Add(a.label);
            foreach (var kv in f.values)
                if (!kv.Value.NullOrEmpty() && taken.Add(kv.Key))
                    args.Add(kv.Value.Named(kv.Key));
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Defs.Look(ref incident, "incident");
            Scribe_Collections.Look(ref group, "group", LookMode.Reference);
            Scribe_Values.Look(ref relation, "relation");
            Scribe_Values.Look(ref dead, "dead");
            Scribe_Values.Look(ref beast, "beast");
            Scribe_Values.Look(ref tale, "tale");
            Scribe_Values.Look(ref host, "host");
            Scribe_Values.Look(ref rite, "rite");
            Scribe_Values.Look(ref guest, "guest");
            Scribe_Values.Look(ref points, "points");
            Scribe_References.Look(ref enemy, "enemy");
            Scribe_References.Look(ref place, "place");
            Scribe_Collections.Look(ref things, "things", LookMode.Reference);
            Scribe_Values.Look(ref spot, "spot", IntVec3.Invalid);
            Scribe_Values.Look(ref xWaiting, "xWaiting");
            Scribe_Values.Look(ref variant, "variant");
            Scribe_Values.Look(ref contest, "contest");
            Scribe_Values.Look(ref scenario, "scenario");
            Scribe_Values.Look(ref placeName, "placeName");
            Scribe_Values.Look(ref note, "note");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (group == null) group = new List<Pawn>();
                group.RemoveAll(p => p == null);
                if (things == null) things = new List<Thing>();
                things.RemoveAll(t => t == null);
            }
        }
    }

    /// <summary>A world event that asks the colony something.</summary>
    public class ChoiceLetter_Happening : ChoiceLetter
    {
        public HappeningContext ctx;
        public bool resolved;

        public override bool CanDismissWithRightClick => false;

        public override IEnumerable<DiaOption> Choices
        {
            get
            {
                var ext = ctx?.Ext;
                if (ArchivedOnly || resolved || ext == null || ext.options.NullOrEmpty() || ctx.Worker == null)
                {
                    yield return Option_Close;
                    yield break;
                }
                for (int i = 0; i < ext.options.Count; i++)
                {
                    int index = i;
                    var o = ext.options[i];
                    // The default answer is always possible, so the letter can never trap the player.
                    string why = i == ext.defaultOption ? null : ctx.Worker.Unavailable(ctx, i) ?? o.effects.Select(e => e.Unavailable(ctx)).FirstOrDefault(r => r != null);
                    // What the answer does, under it and on hover (Dilemmas/DilemmaImpact.cs).
                    yield return DilemmaImpact.Option(ctx, o, ctx.Format(o.label), why, () =>
                    {
                        CouncilGameComponent.Instance?.happenings.Resolve(this, index, false);
                        Find.LetterStack.RemoveLetter(this);
                    });
                }
                // Someone waiting at the edge of the map can't be clicked on: show who they are.
                var x = ctx.stranger;
                if (x != null && !x.Spawned && !x.Dead && !x.Discarded)
                    yield return new DiaOption("CC_Evt_LookAt".Translate(x.LabelShort)) { action = () => Find.WindowStack.Add(new Dialog_InfoCard(x, null)) };
                yield return Option_Postpone;
            }
        }

        /// <summary>Unanswered when the time runs out: the default happens.</summary>
        public override void Removed()
        {
            base.Removed();
            if (!resolved && ctx?.incident != null) CouncilGameComponent.Instance?.happenings.Resolve(this, ctx.Ext?.defaultOption ?? 0, true);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref ctx, "ctx");
            Scribe_Values.Look(ref resolved, "resolved");
        }
    }

    // ===================== Things the tracker keeps =====================

    /// <summary>A fight that was promised: it comes when its time does.</summary>
    public class PendingRaid : IExposable
    {
        public Map map;
        public Faction faction;
        /// <summary>Share of the storyteller's threat points at the time it comes.</summary>
        public float factor = 0.6f;
        public int tick;
        public string label;
        public string text;
        /// <summary>Someone who leads it (a nemesis): joins the raiders when they arrive.</summary>
        public Pawn leader;
        public int postponed;
        /// <summary>Part of the scenario the player chose: comes even with world-event battles switched off.</summary>
        public bool scenario;

        public void ExposeData()
        {
            Scribe_References.Look(ref map, "map");
            Scribe_References.Look(ref faction, "faction");
            Scribe_Values.Look(ref factor, "factor", 0.6f);
            Scribe_Values.Look(ref tick, "tick");
            Scribe_Values.Look(ref label, "label");
            Scribe_Values.Look(ref text, "text");
            Scribe_References.Look(ref leader, "leader");
            Scribe_Values.Look(ref postponed, "postponed");
            Scribe_Values.Look(ref scenario, "scenario");
        }
    }

    /// <summary>A colonist who walked out or was sent away, and might come back one day.</summary>
    public class Departed : IExposable
    {
        public Pawn pawn;
        public string name;
        public int tick;
        public bool quit;
        /// <summary>Rimfolk pinned them in the world (and so may let go).</summary>
        public bool pinned;

        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Values.Look(ref name, "name");
            Scribe_Values.Look(ref tick, "tick");
            Scribe_Values.Look(ref quit, "quit");
            Scribe_Values.Look(ref pinned, "pinned");
        }
    }

    /// <summary>
    /// A prisoner who died in the colony's hands, as an older save kept it: read on load and moved into the world ledger
    /// (Happenings/Grudges.cs keeps them there now).
    /// </summary>
    public class Grudge : IExposable
    {
        public string name;
        public Faction faction;
        public int tick;
        public bool settled;

        public void ExposeData()
        {
            Scribe_Values.Look(ref name, "name");
            Scribe_References.Look(ref faction, "faction");
            Scribe_Values.Look(ref tick, "tick");
            Scribe_Values.Look(ref settled, "settled");
        }
    }

    /// <summary>Visitors who are doing something while they're here: telling tales, mourning, a contest.</summary>
    public class Visit : IExposable
    {
        public string kind;
        public IncidentDef incident;
        public Map map;
        public Pawn x;
        public Pawn a;
        public List<Pawn> group = new List<Pawn>();
        public Thing place;
        public SkillDef skill;
        public int stake;
        public int due;
        public int until;
        public bool done;
        public string text;
        public List<Pawn> touched = new List<Pawn>();
        /// <summary>A champion's bout under way for a fighting challenge (Harm/ChampionBouts.cs); not saved, as bouts aren't.</summary>
        [Unsaved] public OutsiderBout bout;

        public HappeningExtension Ext => incident?.GetModExtension<HappeningExtension>();

        public void ExposeData()
        {
            Scribe_Values.Look(ref kind, "kind");
            Scribe_Defs.Look(ref incident, "incident");
            Scribe_References.Look(ref map, "map");
            Scribe_References.Look(ref x, "x");
            Scribe_References.Look(ref a, "a");
            Scribe_Collections.Look(ref group, "group", LookMode.Reference);
            Scribe_References.Look(ref place, "place");
            Scribe_Defs.Look(ref skill, "skill");
            Scribe_Values.Look(ref stake, "stake");
            Scribe_Values.Look(ref due, "due");
            Scribe_Values.Look(ref until, "until");
            Scribe_Values.Look(ref done, "done");
            Scribe_Values.Look(ref text, "text");
            Scribe_Collections.Look(ref touched, "touched", LookMode.Reference);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (group == null) group = new List<Pawn>();
                if (touched == null) touched = new List<Pawn>();
                group.RemoveAll(p => p == null);
                touched.RemoveAll(p => p == null);
            }
        }
    }

    /// <summary>
    /// Something a world event left behind that a later one can pick up: a deserter who joined, a child taken home, a
    /// champion beaten, a beast felled, pilgrims fed. People in it are kept in the world until it's used or forgotten.
    /// </summary>
    public class Memo : IExposable
    {
        public string kind;
        public Pawn pawn;
        /// <summary>Someone of ours it concerns (who found the lost child, who felled the beast).</summary>
        public Pawn other;
        public Faction faction;
        /// <summary>A def name (the skill of a contest, the kind of a beast).</summary>
        public string def;
        public string text;
        public int tick;
        public bool used;
        public bool pinned;
        /// <summary>Contest: the colony won it.</summary>
        public bool won;
        /// <summary>Debts and hunts (from a scenario): when it next comes due. A debt waiting for an answer: int.MaxValue.</summary>
        public int due;
        /// <summary>
        /// What a kind of memo counts (an order's price, a commission's...). A debt's silver is in its tie (<see cref="tie"/>),
        /// and only here if the ledger had no room for it.
        /// </summary>
        public int amount;
        /// <summary>A debt: its tie in the world ledger (Scenarios/ScenarioDebts.cs), 0 for none.</summary>
        public int tie;
        /// <summary>A hunt: how many more times they come (-1: until the one leading it falls).</summary>
        public int left = -1;
        /// <summary>A hunt: days between attacks.</summary>
        public float everyMin;
        public float everyMax;
        /// <summary>A hunt: times they came. (A debt's times put off are in its tie.)</summary>
        public int times;
        /// <summary>
        /// The colony it belongs to (its settlement's id, <see cref="Homes"/>): the map of whatever made it, else the seat.
        /// Its follow-up comes there while that's still a home (<see cref="HappeningTracker.MapFor"/>); an older memo has
        /// none and goes to the seat.
        /// </summary>
        public int home = HomesMath.None;

        public void ExposeData()
        {
            Scribe_Values.Look(ref kind, "kind");
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_References.Look(ref other, "other");
            Scribe_References.Look(ref faction, "faction");
            Scribe_Values.Look(ref def, "def");
            Scribe_Values.Look(ref text, "text");
            Scribe_Values.Look(ref tick, "tick");
            Scribe_Values.Look(ref used, "used");
            Scribe_Values.Look(ref pinned, "pinned");
            Scribe_Values.Look(ref won, "won");
            Scribe_Values.Look(ref due, "due");
            Scribe_Values.Look(ref amount, "amount");
            Scribe_Values.Look(ref tie, "tie");
            Scribe_Values.Look(ref left, "left", -1);
            Scribe_Values.Look(ref everyMin, "everyMin");
            Scribe_Values.Look(ref everyMax, "everyMax");
            Scribe_Values.Look(ref times, "times");
            Scribe_Values.Look(ref home, "home", HomesMath.None);
        }
    }

    // ===================== The tracker =====================

    /// <summary>
    /// Rimfolk's world events: when the next one comes, fights that were promised, visitors who are busy, the great
    /// beast, and what the colony's past has left behind (people who left, prisoners who died).
    /// </summary>
    public class HappeningTracker : IExposable
    {
        /// <summary>
        /// Rimfolk's own event timer, one per home (a settlement's id and when its next event is due), so each colony
        /// gets its events on its own rhythm.
        /// </summary>
        public Dictionary<int, int> timers = new Dictionary<int, int>();
        /// <summary>
        /// The seat's timer as it carries on: an older save's single timer, kept while there's no home (a gravship in
        /// flight) and taken up by the next seat that has none of its own.
        /// </summary>
        public int nextTick = -1;
        public int lastFiredTick = -999999;
        public HappeningGroup lastGroup = HappeningGroup.Nature;
        public List<PendingRaid> raids = new List<PendingRaid>();
        public List<Departed> departed = new List<Departed>();
        /// <summary>An older save's grudges, read on load and moved into the world ledger (Grudges.cs).</summary>
        private List<Grudge> oldGrudges;
        public List<Visit> visits = new List<Visit>();
        public List<Memo> memos = new List<Memo>();
        /// <summary>Visitors the colony keeps up with, who come back (Happenings/Regulars.cs).</summary>
        public List<Regular> regulars = new List<Regular>();
        public int nextRegularId = 1;
        /// <summary>Quests already noticed (finished ones get a line in the chronicle once).</summary>
        private HashSet<int> seenQuests = new HashSet<int>();
        private bool questsInitialized;
        public Pawn beast;
        public string beastName;
        public int beastTick;
        /// <summary>Someone Rimfolk itself is sending away (a bitter colonist walking out), so the record says "quit".</summary>
        [Unsaved] public Pawn quitting;

        private static CouncilSettings S => ColonyCouncilMod.Settings;

        public static IncidentCategoryDef Category => CouncilDefOf.Council_WorldEvent;

        public static IEnumerable<IncidentDef> All =>
            DefDatabase<IncidentDef>.AllDefsListForReading.Where(d => d.HasModExtension<HappeningExtension>());

        /// <summary>
        /// Puts Rimfolk's colony events where they're drawn from: the storyteller's own pools (everyday events with
        /// Misc, fights with the small threats) or Rimfolk's own category, which only Rimfolk's timer fires. Events
        /// that never fire by chance (a scenario's debt, the founding day) always stay in Rimfolk's category.
        /// </summary>
        public static void ApplyEventMode()
        {
            bool storyteller = S == null || S.eventsFromStoryteller;
            foreach (var d in All)
            {
                var ext = d.GetModExtension<HappeningExtension>();
                d.category = !storyteller || d.baseChance <= 0f ? Category : ext.battle ? IncidentCategoryDefOf.ThreatSmall : IncidentCategoryDefOf.Misc;
            }
            shareCache.Clear();
        }

        /// <summary>At most this share of a storyteller pool is Rimfolk's, so the game's traders, travelers and threats keep coming.</summary>
        private static float MaxShare(IncidentCategoryDef cat) => cat == IncidentCategoryDefOf.ThreatSmall ? 0.25f : 0.5f;

        private static readonly Dictionary<IncidentCategoryDef, (int day, float factor)> shareCache = new Dictionary<IncidentCategoryDef, (int, float)>();

        /// <summary>
        /// The storyteller's weight for one of Rimfolk's events, as a factor on its own base chance: the player's
        /// setting, the share cap for its pool, variety (not the same kind twice running) and, for newcomers, how many
        /// people the storyteller thinks the colony should have.
        /// </summary>
        public static float StorytellerFactor(IncidentDef def)
        {
            var ext = def.GetModExtension<HappeningExtension>();
            float f = Mathf.Clamp(S.storytellerEventWeight, 0.05f, 4f) * ShareFactor(def.category);
            // Every mission offer shares one draw: a new kind takes a share of it (Missions/Workers_MissionNeighbours4.cs).
            f *= MissionPool.Factor(def);
            var t = CouncilGameComponent.Instance?.happenings;
            if (ext != null && t != null && ext.group == t.lastGroup) f *= 0.5f;
            if (ext != null && ext.addsPeople) f *= Mathf.Clamp(StorytellerUtilityPopulation.PopulationIntent + 0.35f, 0.05f, 1.5f);
            // A world event running in the region (a plague, a warlord) makes some likelier and others rarer.
            f *= WorldSituations.EventFactor(def, ext);
            // A quarantine decree shuts the gates to visitors (DecreeWork.VisitorFactor). With more than one colony each
            // colony's own decrees count for its own home, by the event's target (IncidentWorker_Happening.ChanceFactorNow).
            if (ext != null && ext.group == HappeningGroup.Visitors && !ColonyState.Several) f *= DecreeWork.VisitorFactor(null);
            // What the colony is known for draws those who come for it (Immersion/VisitorNotices.cs).
            f *= DrawnFactor(ext, null);
            return f;
        }

        private static float ShareFactor(IncidentCategoryDef cat)
        {
            if (cat == null) return 1f;
            int day = Find.TickManager.TicksGame / GenDate.TicksPerDay;
            if (shareCache.TryGetValue(cat, out var c) && c.day == day) return c.factor;
            float ours = 0f, theirs = 0f;
            foreach (var d in DefDatabase<IncidentDef>.AllDefsListForReading)
            {
                if (d.category != cat) continue;
                if (d.HasModExtension<HappeningExtension>()) ours += d.baseChance * Mathf.Clamp(S.storytellerEventWeight, 0.05f, 4f);
                else
                {
                    try
                    {
                        theirs += Mathf.Max(0f, d.Worker.BaseChanceThisGame);
                    }
                    catch
                    {
                        theirs += d.baseChance;
                    }
                }
            }
            float max = MaxShare(cat);
            float factor = 1f;
            if (ours > 0f && theirs > 0f && ours / (ours + theirs) > max) factor = max / (1f - max) * theirs / ours;
            shareCache[cat] = (day, factor);
            return factor;
        }

        private static int Now => Find.TickManager.TicksGame;

        /// <summary>The colony (the seat, <see cref="Homes.Seat"/>), on the ground; null when it lives only in space, where nobody can walk in.</summary>
        public static Map HomeMap()
        {
            var m = MapFacts.BusiestHome;
            return MapFacts.InVacuum(m) ? null : m;
        }

        /// <summary>
        /// Where a memo's follow-up comes (a guest arriving, goods brought back, an order collected): its colony while
        /// that's still a home on the ground, else the seat (<see cref="HomeMap"/>). Null with no home on the ground.
        /// </summary>
        public static Map MapFor(Memo m)
        {
            var map = Homes.ForMemo(m?.home ?? HomesMath.None);
            return map != null && !MapFacts.InVacuum(map) ? map : HomeMap();
        }

        public void Hourly(CouncilGameComponent comp)
        {
            int now = Now;
            Safe("event raids", () => RaidsDue(now));
            Safe("event visits", () => VisitsHour(comp, now));
            // Visitors the colony keeps up with: the long unseen and the fallen-out are forgotten once a day (Happenings/Regulars.cs).
            if (GenLocalDate.HourOfDay(HomeMap() ?? Find.AnyPlayerHomeMap ?? Find.CurrentMap) == 5) Safe("regulars", Regulars.Daily);
            Safe("great beast", () => HappeningWorker_GreatBeast.Hourly(this, comp));
            // A neighbor's order the colony took on: a reminder, then the collection (even with world events off since).
            Safe("orders", () => HappeningWorker_Order.Hourly(this, comp, now));
            // A commission the colony took on: collected when it's made, or given up at its deadline (Workers_NewEvents.cs).
            Safe("commissions", () => HappeningWorker_Commission.Hourly(this, comp, now));
            // Later parts of the fourth batch: arrivals, caravans and fairs coming back, old memories fading, founding day.
            Safe("living", () => LivingEvents.Hourly(this, comp, now));
            // The storyteller batch's own: ventures, campaigns and gifts coming back (Workers_Storyteller.cs).
            Safe("storyteller events", () => StorytellerEvents.Hourly(this, comp, now));
            // Before the daily tidy-up, so a hunt whose leader just died is told before its memo goes.
            Safe("scenario", () => ScenarioStakes.Hourly(this, comp, now));
            if (now % GenDate.TicksPerDay < GenDate.TicksPerHour) Safe("event memory", Prune);
            Safe("quests", () => Quests(comp));
            // With the storyteller in charge of them, Rimfolk's own timer stays quiet.
            if (!S.worldEvents || S.eventsFromStoryteller) return;
            EventTimers(now);
        }

        /// <summary>
        /// Rimfolk's own timer, one per home: when a home's comes round, an event that fits comes to that home. The seat
        /// carries on the timer it had before there was one per home (<see cref="nextTick"/>), so one colony keeps its
        /// rhythm, and a home lost takes its timer with it.
        /// </summary>
        private void EventTimers(int now)
        {
            var homes = Homes.All;
            int seat = Homes.KeyOf(Homes.Seat);
            if (homes.Count == 0)
            {
                timers.Clear();
                if (nextTick < 0) nextTick = now + Interval();
                return;
            }
            if (timers.Count > 0)
                foreach (int key in timers.Keys.ToList())
                {
                    var m = Homes.MapOf(key);
                    if (m == null || (key != seat && MapFacts.InVacuum(m))) timers.Remove(key);
                }
            if (seat != HomesMath.None && !timers.ContainsKey(seat) && nextTick >= 0) timers[seat] = nextTick;
            foreach (var map in homes)
            {
                int key = Homes.KeyOf(map);
                // Nobody walks in on a home in space: it has no timer of its own, and the seat's waits while it's up there
                // (as the colony's one timer always waited while it lived only in orbit).
                bool space = MapFacts.InVacuum(map);
                if (space && key != seat) continue;
                if (!timers.TryGetValue(key, out int next) || next < 0) next = now + Interval();
                timers[key] = next;
                if (now < GenDate.TicksPerDay * 3 || now < next || space) continue;
                // Not on top of a fight, and not while the colony still has an event to answer.
                if (MapFacts.ThreatOn(map) || WaitingAt(map))
                {
                    timers[key] = now + GenDate.TicksPerHour * 4;
                    continue;
                }
                // Moved on first, so an event that throws can't fire again every hour.
                timers[key] = now + GenDate.TicksPerDay / 2;
                // A remote home's wait is cut by its visitors' share, so they come more often and the rest as before (TravelMath.IntervalFactor).
                if (TryFire(map, null)) timers[key] = now + Mathf.RoundToInt(Interval() * TravelLand.IntervalFactor(map, lastVisitorShare));
            }
            if (seat != HomesMath.None && timers.TryGetValue(seat, out int seatNext)) nextTick = seatNext;
        }

        /// <summary>An event letter nobody has answered yet, about any colony.</summary>
        public static bool Waiting => WaitingAt(null);

        /// <summary>
        /// An event letter nobody has answered yet that holds back the colony of <paramref name="map"/>: one about that home,
        /// or about no home (<see cref="Homes.HoldsBack"/>). One about another home waits without keeping this one quiet.
        /// With one colony, or no map, any unanswered letter does.
        /// </summary>
        public static bool WaitingAt(Map map)
        {
            var letters = Find.LetterStack.LettersListForReading;
            for (int i = 0; i < letters.Count; i++)
                if (letters[i] is ChoiceLetter_Happening h && !h.resolved && Homes.HoldsBack(h.ctx?.map, map)) return true;
            return false;
        }

        private static int Interval() => Mathf.RoundToInt(Mathf.Max(0.5f, S.daysBetweenWorldEvents) * Rand.Range(0.6f, 1.4f) * GenDate.TicksPerDay);

        /// <summary>Fires one world event that fits now (or one of those <paramref name="only"/> allows).</summary>
        public bool TryFire(Map map, Func<IncidentDef, bool> only)
        {
            var parmsByCat = new Dictionary<IncidentCategoryDef, IncidentParms>();
            IncidentParms Parms(IncidentDef d)
            {
                if (!parmsByCat.TryGetValue(d.category, out var p)) parmsByCat[d.category] = p = StorytellerUtility.DefaultParmsNow(d.category, map);
                return p;
            }
            var pool = new List<IncidentDef>();
            foreach (var d in All)
            {
                if (only != null && !only(d)) continue;
                try
                {
                    if (d.TargetAllowed(map) && d.Worker.CanFireNow(Parms(d))) pool.Add(d);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("event check " + d.defName, e);
                }
            }
            // The visitors' share of what could come, as weighed before a remote home's pull (for its next wait: EventTimers).
            float visitors = 0f, all = 0f;
            foreach (var d in pool)
            {
                float w = Weight(d, map, false);
                all += w;
                if (d.GetModExtension<HappeningExtension>()?.group == HappeningGroup.Visitors) visitors += w;
            }
            lastVisitorShare = TravelMath.VisitorShare(visitors, all);
            while (pool.Count > 0)
            {
                var def = pool.RandomElementByWeight(d => Weight(d, map));
                pool.Remove(def);
                if (Find.Storyteller.TryFire(new FiringIncident(def, null, Parms(def)), false)) return true;
            }
            return false;
        }

        /// <summary>The visitors' share of the events that could come at the last try (not saved: a load starts from none).</summary>
        [Unsaved] private float lastVisitorShare;

        private float Weight(IncidentDef d, Map map, bool remote = true)
        {
            var ext = d.GetModExtension<HappeningExtension>();
            float w = d.baseChance * MissionPool.Factor(d);
            // Variety: the same kind of event twice in a row is less likely.
            if (ext.group == lastGroup) w *= 0.35f;
            // Newcomers follow the storyteller's sense of how big the colony should be.
            if (ext.addsPeople) w *= Mathf.Clamp(StorytellerUtilityPopulation.PopulationIntent + 0.35f, 0.05f, 1.5f);
            w *= WorldSituations.EventFactor(d, ext);
            // A quarantine or open gates decree, as in the storyteller's own pools (StorytellerFactor).
            if (ext.group == HappeningGroup.Visitors) w *= DecreeWork.VisitorFactor(map);
            // A remote home draws more visitors (World/Region/TravelLand.cs): up to 60% more at the most remote.
            if (remote && ext.group == HappeningGroup.Visitors) w *= TravelLand.VisitorFactor(map);
            w *= DrawnFactor(ext, map);
            // A town struggling in this home's reach asks for help more often (World/Region/RiseFallEffects.cs).
            w *= RiseFallEffects.EventFactor(d, map);
            // A colony (or one of its people by name) known for something draws the world's events of its kind (Diplomacy/Renown.cs).
            w *= Renown.EventFactor(d, map);
            return Mathf.Max(0.001f, w);
        }

        /// <summary>Two and a half times as likely while the colony is known for something the event comes for (HappeningExtension.drawnBy).</summary>
        public static float DrawnFactor(HappeningExtension ext, Map map)
        {
            if (ext?.drawnBy == null || ext.drawnBy.Count == 0) return 1f;
            // What the home it would come to is known for (each colony its own character; the seat's for none).
            var ch = CharacterTracker.For(map);
            if (ch == null) return 1f;
            foreach (var tag in ext.drawnBy)
                if (ch.Is(tag)) return 2.5f;
            return 1f;
        }

        public void Notify_Fired(IncidentDef def, HappeningExtension ext)
        {
            lastFiredTick = Now;
            lastGroup = ext.group;
        }

        // ---------------- Answers ----------------

        public void Resolve(ChoiceLetter_Happening letter, int index, bool byDefault)
        {
            if (letter == null || letter.resolved) return;
            letter.resolved = true;
            var ctx = letter.ctx;
            var ext = ctx?.Ext;
            var worker = ctx?.Worker;
            // The event's mod part was removed in the meantime: nothing to do.
            if (ext == null || worker == null || ext.options.NullOrEmpty()) return;
            int i = Mathf.Clamp(index, 0, ext.options.Count - 1);
            var option = ext.options[i];
            var comp = CouncilGameComponent.Instance;
            try
            {
                if (ctx.why == null) ctx.why = ctx.incident.LabelCap;
                ctx.cause = "CC_Cause_Event".Translate(ctx.incident.LabelCap, ctx.Format(option.label)).Resolve();
                using var _cause = Causes.Push(ctx.cause);
                // Whoever the answer calls on (a doctor, a talker) first, so their skill counts against the risk.
                DilemmaChoices.PickHelpers(option, ctx);
                bool risk = option.riskChance > 0f && Rand.Chance(worker.RiskChance(ctx, i, option.riskChance));
                // What the answer sets going on the map comes first, so it can hold the moods of those taking part.
                List<PlayOut> plays;
                using (PlayOuts.Collect())
                {
                    if (!risk || !option.riskReplaces)
                        foreach (var e in PlayOuts.Ordered(option.effects))
                            if (!byDefault || !e.onlyIfChosen) Safe(e, ctx);
                    if (risk && option.riskEffects != null)
                        foreach (var e in PlayOuts.Ordered(option.riskEffects))
                            if (!byDefault || !e.onlyIfChosen) Safe(e, ctx);
                    plays = PlayOuts.Collected();
                }
                string outcome = DilemmaChoices.Outcome(option, risk, worker.TextTags(ctx));
                ctx.plays = plays;
                outcome = worker.Resolved(ctx, i, risk, byDefault, outcome);
                // A cold answer to a visitor the colony keeps up with loosens the thread (Happenings/Regulars.cs).
                if (!byDefault) Regulars.Answered(ctx, option);
                ctx.plays = null;
                if (comp != null) CharacterTracker.For(ctx.map)?.Shift(byDefault ? option.ways?.Scaled(0.5f) : option.ways, "CC_Ways_Why_Event".Translate(ctx.incident.label, ctx.Format(option.label)).Resolve());
                Setbacks.Notify_Answer(ctx, option.ways, byDefault, risk, ctx.incident.label, ctx.Format(option.label));
                string told = ctx.Format(outcome);
                // What the answer set going on the map tells its outcome when it's over; until then, only when it will be.
                if (!told.NullOrEmpty() && !PlayOuts.TellWhenDone(plays, told, byDefault ? "CC_Evt_NoAnswer".Translate().ToString() : null))
                {
                    if (byDefault) told = "CC_Evt_NoAnswer".Translate() + " " + told;
                    Tell.Message(NewsKind.World, Importance.Notable, ctx.map, told, new LookTargets(new[] { ctx.a, ctx.b, ctx.stranger }.Where(p => p != null && p.Spawned)), MessageTypeDefOf.NeutralEvent);
                }
                // How it went, in the history's answers' log; a notable event's answer is a line of the chronicle too.
                Answers.Record("event", ctx.incident.LabelCap, ctx.Format(option.label), told, ctx.a, ctx.faction, ctx.map, byDefault, risk, plays: plays, minor: !ext.notable);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("event answer " + ctx.incident?.defName, e);
            }
            finally
            {
                try
                {
                    worker.Cleanup(ctx);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("event cleanup " + ctx.incident?.defName, e);
                }
                // The letter's text is written; let go of the people so old saves don't hold them.
                ctx.plays = null;
                ctx.a = ctx.b = ctx.pet = ctx.stranger = null;
                ctx.group.Clear();
                ctx.things.Clear();
                ctx.map = null;
                ctx.place = null;
            }
        }

        public static void Safe(DilemmaEffect e, DilemmaContext ctx)
        {
            try
            {
                e.Apply(ctx);
            }
            catch (Exception ex)
            {
                CouncilLog.ErrorOnce("event effect " + e.GetType().Name, ex);
            }
        }

        private static void Safe(string context, Action a)
        {
            try
            {
                a();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce(context, e);
            }
        }

        // ---------------- Promised fights ----------------

        private void Unpin(Pawn p)
        {
            // Not while anything else of Rimfolk keeps them: a nemesis, another memo (a hunt they lead), a world event (a
            // warlord), a story, a mission, a notable, a regular, a camp that may come back (WorldLedger.Held).
            if (p == null || WorldLedger.Held(p)) return;
            Find.WorldPawns.ForcefullyKeptPawns.Remove(p);
        }

        /// <summary>A memo still waiting on this pawn (a hunt they lead, a debt they're owed): keep them in the world.</summary>
        public bool Holds(Pawn p) => p != null && memos.Any(m => !m.used && m.pawn == p);

        public void AddRaid(Map map, Faction faction, float factor, float delayHours, string label, string text, Pawn leader = null, bool scenario = false)
        {
            if (map == null || faction == null) return;
            raids.Add(new PendingRaid
            {
                map = map, faction = faction, factor = factor, tick = Now + Mathf.RoundToInt(delayHours * GenDate.TicksPerHour),
                label = label, text = text, leader = leader, scenario = scenario
            });
            if (leader != null && Find.WorldPawns.Contains(leader)) Find.WorldPawns.ForcefullyKeptPawns.Add(leader);
        }

        private void RaidsDue(int now)
        {
            foreach (var r in raids.Where(r => now >= r.tick).ToList())
            {
                raids.Remove(r);
                // The map is gone, the enemy is gone or at peace, or fights were switched off since: the threat never comes.
                if (r.map == null || !Find.Maps.Contains(r.map) || r.faction == null || r.faction.defeated || !r.faction.HostileTo(Faction.OfPlayer)
                    || !(r.scenario ? IncidentWorker_Happening.ScenarioFightsPossible : IncidentWorker_Happening.FightsPossible))
                {
                    Unpin(r.leader);
                    continue;
                }
                // Not on top of another fight, nor right after one: they wait a little.
                bool busy = MapFacts.ThreatOn(r.map) || now - r.map.storyState.LastThreatBigTick < GenDate.TicksPerDay;
                if (busy && r.postponed < 4)
                {
                    r.postponed++;
                    r.tick = now + GenDate.TicksPerHour * 6;
                    raids.Add(r);
                    continue;
                }
                float factor = r.factor * (r.scenario ? Mathf.Max(0.1f, S.scenarioThreat) : 1f);
                bool sent = Raids.Send(r.map, r.faction, StorytellerUtility.DefaultThreatPointsNow(r.map) * factor, r.label, r.text, r.leader);
                // The raid's own letter told the story: counted as their return without a second letter.
                var comp = CouncilGameComponent.Instance;
                var n = sent && r.leader != null && r.leader.Spawned ? comp?.nemeses.For(r.leader) : null;
                if (n != null && n.announced) comp.nemeses.Notify_LedRaid(comp, n);
            }
        }

        // ---------------- Visitors with something to do ----------------

        public Visit AddVisit(string kind, IncidentDef incident, Map map, Pawn x, Pawn a, IEnumerable<Pawn> group, int untilTick)
        {
            var v = new Visit { kind = kind, incident = incident, map = map, x = x, a = a, until = untilTick };
            if (group != null) v.group.AddRange(group.Where(p => p != null));
            visits.Add(v);
            return v;
        }

        private void VisitsHour(CouncilGameComponent comp, int now)
        {
            foreach (var v in visits.ToList())
            {
                bool keep;
                try
                {
                    switch (v.kind)
                    {
                        case "tales": keep = HappeningWorker_Storyteller.Hour(v, comp, now); break;
                        case "grave": keep = HappeningWorker_GraveVisitors.Hour(v, comp, now); break;
                        case "contest": keep = HappeningWorker_Champion.Hour(v, comp, now); break;
                        case HappeningWorker_TableGame.Kind: keep = HappeningWorker_TableGame.Hour(v, comp, now); break;
                        default: keep = false; break;
                    }
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("event visit " + v.kind, e);
                    keep = false;
                }
                if (!keep || now > v.until + GenDate.TicksPerDay) visits.Remove(v);
            }
        }

        // ---------------- What the past leaves behind ----------------

        /// <summary>A colonist was banished or walked out (Harmony hook on banishing).</summary>
        public void Notify_Banished(Pawn p)
        {
            if (p == null || p.Dead || !p.RaceProps.Humanlike) return;
            var old = departed.FirstOrDefault(d => d.pawn == p);
            if (old != null) departed.Remove(old);
            bool pinned = WorldLedger.Keep(p, KeepFor.Departed) || (old?.pinned ?? false);
            departed.Add(new Departed { pawn = p, name = p.LabelShort, tick = Now, quit = p == quitting || (old?.quit ?? false), pinned = pinned });
            foreach (var d in departed.Where(d => !AskedAbout(d.pawn)).Take(Mathf.Max(0, departed.Count - 6)).ToList()) Forget(d);
        }

        /// <summary>The stranger of an event letter still waiting for an answer.</summary>
        private static bool AskedAbout(Pawn p) =>
            p != null && Find.LetterStack.LettersListForReading.Any(l => l is ChoiceLetter_Happening h && !h.resolved && h.ctx?.stranger == p);

        public void Forget(Departed d)
        {
            departed.Remove(d);
            if (d.pawn != null && !departed.Any(x => x.pawn == d.pawn)) WorldLedger.Release(d.pawn, KeepFor.Departed);
            if (d.pinned && d.pawn != null && !AskedAbout(d.pawn)) Unpin(d.pawn);
        }

        /// <summary>Who someone was just before they died (taken before the death, acted on after it).</summary>
        public struct DeathNote
        {
            public bool beast;
            public Faction prisonerOf;
            public string name;
            /// <summary>An enemy falling in a fight at home (for the victory count).</summary>
            public bool foe;
            /// <summary>Finished off after the fight, lying downed: counted, but nobody's glory.</summary>
            public bool afterFight;
            /// <summary>A wild animal on a home map (a hunt's kill, for the orphaned young), and where it fell.</summary>
            public bool wild;
            public IntVec3 cell;
            /// <summary>Where they died (the home whose fight it counts in).</summary>
            public Map map;
        }

        public DeathNote Before(Pawn victim)
        {
            var note = new DeathNote { beast = victim == beast, name = victim.LabelShort };
            note.foe = victim.Spawned && victim.Map.IsPlayerHome && !victim.IsPrisoner && victim.HostileTo(Faction.OfPlayer);
            note.afterFight = note.foe && victim.Downed && !GenHostility.AnyHostileActiveThreatToPlayer(victim.Map, false, false);
            note.wild = victim.Spawned && victim.Map.IsPlayerHome && victim.RaceProps.Animal && victim.Faction == null;
            note.cell = victim.Spawned ? victim.Position : IntVec3.Invalid;
            note.map = victim.MapHeld;
            if (victim.RaceProps.Humanlike && victim.IsPrisonerOfColony && victim.Faction != null && !victim.Faction.IsPlayer && !victim.Faction.Hidden)
                note.prisonerOf = victim.Faction;
            return note;
        }

        /// <summary>Someone died (Harmony hook): a prisoner in our hands, or the great beast.</summary>
        public void Notify_Killed(Pawn victim, DamageInfo? dinfo, DeathNote note)
        {
            if (victim == null) return;
            if (note.foe) CouncilGameComponent.Instance?.social.Notify_EnemyFell(note.afterFight ? null : dinfo?.Instigator as Pawn, note.map);
            if (note.beast) HappeningWorker_GreatBeast.Slain(this, victim, dinfo);
            if (note.wild && !note.beast) HappeningWorker_Orphan.Killed(this, victim, dinfo, note.cell);
            if (note.prisonerOf != null) Grudges.Add(note.prisonerOf, note.name, Now);
        }

        // ---------------- Memos for later events ----------------

        /// <summary>Notes something a later event can pick up; <paramref name="map"/> is where it happened (its colony: that home, else the seat).</summary>
        public Memo Remember(string kind, Pawn pawn, Faction faction = null, string def = null, string text = null, bool won = false, Pawn other = null, Map map = null)
        {
            var m = new Memo { kind = kind, pawn = pawn, other = other, faction = faction, def = def, text = text, tick = Now, won = won, home = Homes.KeyForMemo(map) };
            // Kept in the world while a memo waits on them; the game told (pinned) unless they're dead or the colony's own.
            if (pawn != null)
            {
                bool tell = !pawn.Dead && pawn.Faction != Faction.OfPlayer;
                bool added = WorldLedger.Keep(pawn, KeepFor.Memo, tell);
                if (tell) m.pinned = added;
            }
            memos.Add(m);
            while (memos.Count > 24 && memos.Any(x => !Keeps(x)))
                Drop(memos.Where(x => !Keeps(x)).OrderBy(x => x.used ? 0 : 1).ThenBy(x => x.tick).First());
            return m;
        }

        /// <summary>
        /// Unused memos of this kind at least <paramref name="minDays"/> old; with a <paramref name="map"/>, only those of its
        /// colony (an older memo, or one of a home no longer ours, is the seat's: <see cref="Homes.MemoFor"/>), so a later
        /// event picks up only what its own colony left. With one colony that is every memo.
        /// </summary>
        public IEnumerable<Memo> Memos(string kind, float minDays = 0f, Map map = null) =>
            memos.Where(m => m.kind == kind && !m.used && Now - m.tick >= minDays * GenDate.TicksPerDay && (map == null || Homes.MemoFor(m.home, map)));

        /// <summary>A later event picked it up: it won't come round again.</summary>
        public void Use(Memo m)
        {
            if (m == null) return;
            // A debt still owed when its memo goes (its people gone): over, nobody's fault.
            if (m.kind == "debt") Debts.Close(m, Tie.State.Ended);
            m.used = true;
            // Still being asked about in a letter: the pin stays until the memo is dropped.
            if (m.pinned && m.pawn != null && AskedAbout(m.pawn)) return;
            if (m.pawn != null && !Holds(m.pawn)) WorldLedger.Release(m.pawn, KeepFor.Memo);
            if (m.pinned && m.pawn != null) Unpin(m.pawn);
            m.pinned = false;
        }

        private void Drop(Memo m)
        {
            Use(m);
            memos.Remove(m);
        }

        /// <summary>Finished quests get a line in the chronicle (and a little pride when they went well).</summary>
        private void Quests(CouncilGameComponent comp)
        {
            var all = Find.QuestManager?.QuestsListForReading;
            if (all == null) return;
            if (!questsInitialized)
            {
                // Quests finished before Rimfolk was keeping count are history already.
                foreach (var q in all) if (q.State != QuestState.NotYetAccepted && q.State != QuestState.Ongoing) seenQuests.Add(q.id);
                questsInitialized = true;
                return;
            }
            foreach (var q in all)
            {
                // Only quests the colony chose to take on (not hidden ones, nor ones that started on their own).
                if (q.hidden || q.hiddenInUI || q.initiallyAccepted || !q.EverAccepted || seenQuests.Contains(q.id)) continue;
                if (q.State != QuestState.EndedSuccess && q.State != QuestState.EndedFailed) continue;
                seenQuests.Add(q.id);
                if (S.worldReactions) WorldReactions.QuestEnded(comp, q.name, q.State == QuestState.EndedSuccess);
            }
        }

        /// <summary>
        /// Memos that are promises still running (a scenario's hunt or debt, an order, a caravan or a guest on the way):
        /// never dropped to make room, or the goods and people they stand for would vanish without a word.
        /// </summary>
        private static bool Keeps(Memo m) => ScenarioStakes.Lasting(m) || LivingEvents.IsPending(m) || StorytellerEvents.IsPending(m) || (m != null && !m.used && (m.kind == "order" || m.kind == HappeningWorker_Commission.Kind));

        private void Prune()
        {
            int now = Now;
            foreach (var d in departed.Where(d => d.pawn == null || d.pawn.Dead || d.pawn.Discarded || d.pawn.Faction == Faction.OfPlayer
                                                   || (now - d.tick > GenDate.TicksPerYear * 2 && !AskedAbout(d.pawn))).ToList())
                Forget(d);
            Grudges.Prune(now);
            foreach (var m in memos.Where(m => (m.used && now - m.tick > GenDate.TicksPerDay)
                                               || (now - m.tick > GenDate.TicksPerYear * (m.kind == "child" ? 10 : 3) && !Keeps(m))
                                               || (m.pawn != null && (m.pawn.Dead || m.pawn.Discarded) && !Keeps(m))).ToList())
                Drop(m);
        }

        /// <summary>Once the save has loaded: what an older save kept here that is a tie now goes into the world ledger.</summary>
        public void MoveToLedger()
        {
            if (oldGrudges != null)
            {
                Grudges.Adopt(oldGrudges);
                oldGrudges = null;
            }
            foreach (var m in memos)
                if (m != null && m.kind == "debt" && !m.used && m.tie <= 0)
                    Debts.Adopt(m, Now);
            foreach (var r in regulars) r?.MoveBond();
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref nextTick, "nextTick", -1);
            Scribe_Collections.Look(ref timers, "timers", LookMode.Value, LookMode.Value);
            Scribe_Values.Look(ref lastFiredTick, "lastFiredTick", -999999);
            Scribe_Values.Look(ref lastGroup, "lastGroup");
            Scribe_Collections.Look(ref raids, "raids", LookMode.Deep);
            Scribe_Collections.Look(ref departed, "departed", LookMode.Deep);
            if (Scribe.mode != LoadSaveMode.Saving) Scribe_Collections.Look(ref oldGrudges, "grudges", LookMode.Deep);
            Scribe_Collections.Look(ref visits, "visits", LookMode.Deep);
            Scribe_Collections.Look(ref memos, "memos", LookMode.Deep);
            Scribe_Collections.Look(ref regulars, "regulars", LookMode.Deep);
            Scribe_Values.Look(ref nextRegularId, "nextRegularId", 1);
            // Quests already told of, by id: only those the game still keeps can come up again (Quests walks the same list).
            if (Scribe.mode == LoadSaveMode.Saving && seenQuests != null && seenQuests.Count > 0)
            {
                var all = Find.QuestManager?.QuestsListForReading;
                if (all != null)
                {
                    var ids = new HashSet<int>();
                    foreach (var q in all) ids.Add(q.id);
                    seenQuests.RemoveWhere(id => !ids.Contains(id));
                }
            }
            Scribe_Collections.Look(ref seenQuests, "seenQuests", LookMode.Value);
            Scribe_Values.Look(ref questsInitialized, "questsInitialized");
            Scribe_References.Look(ref beast, "beast");
            Scribe_Values.Look(ref beastName, "beastName");
            Scribe_Values.Look(ref beastTick, "beastTick");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (timers == null) timers = new Dictionary<int, int>();
                if (raids == null) raids = new List<PendingRaid>();
                if (departed == null) departed = new List<Departed>();
                oldGrudges?.RemoveAll(g => g == null || g.faction == null);
                if (visits == null) visits = new List<Visit>();
                if (memos == null) memos = new List<Memo>();
                if (regulars == null) regulars = new List<Regular>();
                regulars.RemoveAll(r => r == null || r.pawn == null);
                if (seenQuests == null) seenQuests = new HashSet<int>();
                memos.RemoveAll(m => m == null || m.kind == null);
                raids.RemoveAll(r => r == null || r.map == null || r.faction == null);
                departed.RemoveAll(d => d == null || d.pawn == null);
                visits.RemoveAll(v => v == null || v.incident == null || v.map == null);
            }
        }
    }

    public static class HappeningText
    {
        /// <summary>A skill as a noun ("growing", "animal handling"); for contests, what the contest is ("marksmanship", "riddles").</summary>
        public static string SkillNoun(SkillDef skill, bool contest = false)
        {
            if (skill == null) return "";
            if (contest && ("CC_SkillContest_" + skill.defName).CanTranslate()) return ("CC_SkillContest_" + skill.defName).Translate().Resolve();
            if (("CC_SkillNoun_" + skill.defName).CanTranslate()) return ("CC_SkillNoun_" + skill.defName).Translate().Resolve();
            return skill.label;
        }
    }
}
