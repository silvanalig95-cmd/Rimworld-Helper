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
    /// {FACTION}, {ENEMY}, {COUNT}, {THING}, {SKILL}, {RELATION}, {DEAD} and {BEAST}. Lines can carry era tags
    /// ([lowtech], [!lowtech], [industrial|spacer]) and event tags (the colonist's past, the skill, the season).
    /// </summary>
    public class HappeningExtension : DefModExtension
    {
        public HappeningGroup group = HappeningGroup.Visitors;
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
        /// <summary>Whoever came along with X (pilgrims, mourners, a challenger's friends).</summary>
        public List<Pawn> group = new List<Pawn>();
        /// <summary>What X is to A ("sister").</summary>
        public string relation;
        /// <summary>A name from the past: the dead, the executed.</summary>
        public string dead;
        public string beast;
        /// <summary>A story told about the colony, or what a scholar studies.</summary>
        public string tale;
        public float points;
        /// <summary>Who would come to fight.</summary>
        public Faction enemy;
        /// <summary>A grave, a cache.</summary>
        public Thing place;
        /// <summary>X is a world pawn until the colony answers (joins at the edge of the map).</summary>
        public bool xWaiting;
        public int variant;

        public HappeningExtension Ext => incident?.GetModExtension<HappeningExtension>();

        public IncidentWorker_Happening Worker => incident?.Worker as IncidentWorker_Happening;

        protected override void AddArgs(List<NamedArgument> args)
        {
            args.Add((relation ?? "").Named("RELATION"));
            args.Add((dead ?? "").Named("DEAD"));
            args.Add((beast ?? "").Named("BEAST"));
            args.Add((enemy?.Name ?? "").Named("ENEMY"));
            args.Add((tale ?? "").Named("TALE"));
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
            Scribe_Values.Look(ref points, "points");
            Scribe_References.Look(ref enemy, "enemy");
            Scribe_References.Look(ref place, "place");
            Scribe_Values.Look(ref xWaiting, "xWaiting");
            Scribe_Values.Look(ref variant, "variant");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (group == null) group = new List<Pawn>();
                group.RemoveAll(p => p == null);
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
                    var opt = new DiaOption(ctx.Format(o.label));
                    // The default answer is always possible, so the letter can never trap the player.
                    string why = i == ext.defaultOption ? null : ctx.Worker.Unavailable(ctx, i) ?? o.effects.Select(e => e.Unavailable(ctx)).FirstOrDefault(r => r != null);
                    if (why != null) opt.Disable(why);
                    opt.action = () =>
                    {
                        CouncilGameComponent.Instance?.happenings.Resolve(this, index, false);
                        Find.LetterStack.RemoveLetter(this);
                    };
                    opt.resolveTree = true;
                    yield return opt;
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
        }
    }

    /// <summary>A colonist who walked out or was sent away, and might come back one day.</summary>
    public class Departed : IExposable
    {
        public Pawn pawn;
        public string name;
        public int tick;
        public bool quit;

        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Values.Look(ref name, "name");
            Scribe_Values.Look(ref tick, "tick");
            Scribe_Values.Look(ref quit, "quit");
        }
    }

    /// <summary>A prisoner who died in the colony's hands: their people may not forget.</summary>
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

    // ===================== The tracker =====================

    /// <summary>
    /// Rimfolk's world events: when the next one comes, fights that were promised, visitors who are busy, the great
    /// beast, and what the colony's past has left behind (people who left, prisoners who died).
    /// </summary>
    public class HappeningTracker : IExposable
    {
        public int nextTick = -1;
        public int lastFiredTick = -999999;
        private HappeningGroup lastGroup = HappeningGroup.Nature;
        public List<PendingRaid> raids = new List<PendingRaid>();
        public List<Departed> departed = new List<Departed>();
        public List<Grudge> grudges = new List<Grudge>();
        public List<Visit> visits = new List<Visit>();
        public Pawn beast;
        public string beastName;
        public int beastTick;
        /// <summary>Someone Rimfolk itself is sending away (a bitter colonist walking out), so the record says "quit".</summary>
        [Unsaved] public Pawn quitting;

        private static CouncilSettings S => ColonyCouncilMod.Settings;

        public static IncidentCategoryDef Category => CouncilDefOf.Council_WorldEvent;

        public static IEnumerable<IncidentDef> All =>
            DefDatabase<IncidentDef>.AllDefsListForReading.Where(d => d.category == Category && d.HasModExtension<HappeningExtension>());

        private static int Now => Find.TickManager.TicksGame;

        public static Map HomeMap() =>
            Find.Maps.Where(m => m.IsPlayerHome).OrderByDescending(m => m.mapPawns.FreeColonistsSpawnedCount).FirstOrDefault();

        public void Hourly(CouncilGameComponent comp)
        {
            int now = Now;
            Safe("event raids", () => RaidsDue(now));
            Safe("event visits", () => VisitsHour(comp, now));
            Safe("great beast", () => HappeningWorker_GreatBeast.Hourly(this, comp));
            if (now % GenDate.TicksPerDay < GenDate.TicksPerHour) Safe("event memory", Prune);
            if (!S.worldEvents) return;
            if (nextTick < 0) nextTick = now + Interval();
            if (now < GenDate.TicksPerDay * 3 || now < nextTick) return;
            var map = HomeMap();
            if (map == null) return;
            // Not on top of a fight, and not while the colony still has an event to answer.
            if (GenHostility.AnyHostileActiveThreatToPlayer(map, false, false) || Waiting)
            {
                nextTick = now + GenDate.TicksPerHour * 4;
                return;
            }
            nextTick = TryFire(map, null) ? now + Interval() : now + GenDate.TicksPerDay / 2;
        }

        /// <summary>An event letter nobody has answered yet.</summary>
        public static bool Waiting => Find.LetterStack.LettersListForReading.Any(l => l is ChoiceLetter_Happening h && !h.resolved);

        private static int Interval() => Mathf.RoundToInt(Mathf.Max(0.5f, S.daysBetweenWorldEvents) * Rand.Range(0.6f, 1.4f) * GenDate.TicksPerDay);

        /// <summary>Fires one world event that fits now (or one of those <paramref name="only"/> allows).</summary>
        public bool TryFire(Map map, Func<IncidentDef, bool> only)
        {
            var parms = StorytellerUtility.DefaultParmsNow(Category, map);
            var pool = new List<IncidentDef>();
            foreach (var d in All)
            {
                if (only != null && !only(d)) continue;
                try
                {
                    if (d.TargetAllowed(map) && d.Worker.CanFireNow(parms)) pool.Add(d);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("event check " + d.defName, e);
                }
            }
            while (pool.Count > 0)
            {
                var def = pool.RandomElementByWeight(Weight);
                pool.Remove(def);
                if (Find.Storyteller.TryFire(new FiringIncident(def, null, parms), false)) return true;
            }
            return false;
        }

        private float Weight(IncidentDef d)
        {
            var ext = d.GetModExtension<HappeningExtension>();
            float w = d.baseChance;
            // Variety: the same kind of event twice in a row is less likely.
            if (ext.group == lastGroup) w *= 0.35f;
            // Newcomers follow the storyteller's sense of how big the colony should be.
            if (ext.addsPeople) w *= Mathf.Clamp(StorytellerUtilityPopulation.PopulationIntent + 0.35f, 0.05f, 1.5f);
            return Mathf.Max(0.001f, w);
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
                ctx.cause = "CC_Cause_Event".Translate(ctx.incident.LabelCap, ctx.Format(option.label)).Resolve();
                using var _cause = Causes.Push(ctx.cause);
                bool risk = option.riskChance > 0f && Rand.Chance(worker.RiskChance(ctx, i, option.riskChance));
                if (!risk || !option.riskReplaces)
                    foreach (var e in option.effects) Safe(e, ctx);
                string outcome = option.outcome;
                if (risk)
                {
                    if (option.riskEffects != null) foreach (var e in option.riskEffects) Safe(e, ctx);
                    if (!option.riskOutcome.NullOrEmpty()) outcome = option.riskOutcome;
                }
                outcome = worker.Resolved(ctx, i, risk, byDefault, outcome);
                comp?.character.Shift(byDefault ? option.ways?.Scaled(0.5f) : option.ways, "CC_Ways_Why_Event".Translate(ctx.incident.label, ctx.Format(option.label)).Resolve());
                string told = ctx.Format(outcome);
                if (byDefault) told = "CC_Evt_NoAnswer".Translate() + " " + told;
                if (!told.NullOrEmpty())
                    Messages.Message(told, new LookTargets(new[] { ctx.a, ctx.b, ctx.stranger }.Where(p => p != null && p.Spawned)), MessageTypeDefOf.NeutralEvent, false);
                if (ext.notable)
                    comp?.AddChronicle(new ChronicleEntry(ChronicleKind.World, "CC_Chronicle_EventChoice".Translate(ctx.incident.LabelCap, ctx.Format(option.label)).Resolve(), ctx.a));
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
                ctx.a = ctx.b = ctx.pet = ctx.stranger = null;
                ctx.group.Clear();
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

        public void AddRaid(Map map, Faction faction, float factor, float delayHours, string label, string text, Pawn leader = null)
        {
            if (map == null || faction == null) return;
            raids.Add(new PendingRaid
            {
                map = map, faction = faction, factor = factor, tick = Now + Mathf.RoundToInt(delayHours * GenDate.TicksPerHour),
                label = label, text = text, leader = leader
            });
            if (leader != null && Find.WorldPawns.Contains(leader)) Find.WorldPawns.ForcefullyKeptPawns.Add(leader);
        }

        private void RaidsDue(int now)
        {
            foreach (var r in raids.Where(r => now >= r.tick).ToList())
            {
                raids.Remove(r);
                if (r.map == null || !Find.Maps.Contains(r.map) || r.faction == null || r.faction.defeated || !r.faction.HostileTo(Faction.OfPlayer)) continue;
                // Switched off since, or the storyteller no longer sends fights: the threat never comes.
                if (!S.worldEvents || !S.worldEventBattles || Find.Storyteller.difficulty.threatScale <= 0.01f) continue;
                // Not on top of another fight: they wait a little.
                if (GenHostility.AnyHostileActiveThreatToPlayer(r.map, false, false) && r.postponed < 4)
                {
                    r.postponed++;
                    r.tick = now + GenDate.TicksPerHour * 6;
                    raids.Add(r);
                    continue;
                }
                Raids.Send(r.map, r.faction, StorytellerUtility.DefaultThreatPointsNow(r.map) * r.factor, r.label, r.text, r.leader);
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
            if (p == null || !p.RaceProps.Humanlike || !People.HasInnerLife(p)) return;
            departed.RemoveAll(d => d.pawn == p);
            departed.Add(new Departed { pawn = p, name = p.LabelShort, tick = Now, quit = p == quitting });
            Find.WorldPawns.ForcefullyKeptPawns.Add(p);
            while (departed.Count > 6) Forget(departed[0]);
        }

        public void Forget(Departed d)
        {
            departed.Remove(d);
            if (d.pawn != null && Find.WorldPawns.ForcefullyKeptPawns.Contains(d.pawn) && !(CouncilGameComponent.Instance?.nemeses.IsNemesis(d.pawn) ?? false))
                Find.WorldPawns.ForcefullyKeptPawns.Remove(d.pawn);
        }

        /// <summary>Someone died (Harmony hook): a prisoner in our hands, or the great beast.</summary>
        public void Notify_Killed(Pawn victim, DamageInfo? dinfo)
        {
            if (victim == null) return;
            if (victim == beast) HappeningWorker_GreatBeast.Slain(this, victim, dinfo);
            if (victim.RaceProps.Humanlike && victim.IsPrisonerOfColony && victim.Faction != null && !victim.Faction.IsPlayer && !victim.Faction.Hidden)
            {
                grudges.Add(new Grudge { name = victim.LabelShort, faction = victim.Faction, tick = Now });
                while (grudges.Count > 10) grudges.RemoveAt(0);
            }
        }

        private void Prune()
        {
            int now = Now;
            foreach (var d in departed.Where(d => d.pawn == null || d.pawn.Dead || d.pawn.Discarded || d.pawn.Faction == Faction.OfPlayer
                                                   || now - d.tick > GenDate.TicksPerYear * 2).ToList())
                Forget(d);
            grudges.RemoveAll(g => g.settled || g.faction == null || g.faction.defeated || now - g.tick > GenDate.TicksPerYear * 2);
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref nextTick, "nextTick", -1);
            Scribe_Values.Look(ref lastFiredTick, "lastFiredTick", -999999);
            Scribe_Values.Look(ref lastGroup, "lastGroup");
            Scribe_Collections.Look(ref raids, "raids", LookMode.Deep);
            Scribe_Collections.Look(ref departed, "departed", LookMode.Deep);
            Scribe_Collections.Look(ref grudges, "grudges", LookMode.Deep);
            Scribe_Collections.Look(ref visits, "visits", LookMode.Deep);
            Scribe_References.Look(ref beast, "beast");
            Scribe_Values.Look(ref beastName, "beastName");
            Scribe_Values.Look(ref beastTick, "beastTick");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (raids == null) raids = new List<PendingRaid>();
                if (departed == null) departed = new List<Departed>();
                if (grudges == null) grudges = new List<Grudge>();
                if (visits == null) visits = new List<Visit>();
                raids.RemoveAll(r => r == null || r.map == null || r.faction == null);
                departed.RemoveAll(d => d == null || d.pawn == null);
                grudges.RemoveAll(g => g == null || g.faction == null);
                visits.RemoveAll(v => v == null || v.incident == null || v.map == null);
            }
        }
    }
}
