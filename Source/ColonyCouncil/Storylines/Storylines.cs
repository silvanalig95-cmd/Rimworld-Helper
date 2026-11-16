using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // Storylines (docs/quests-proposals.md; DESIGN.md, "Quests and storylines"): a run of quests told over weeks. Each part
    // is a mission in the Quests tab (Missions.cs), titled "Part 2: ..." with the storyline's name over its description,
    // which opens with what happened before. When a part ends (Missions.Finish tells MissionEnded), the storyline's own
    // rules (StorylineMath.Next) pick the next part and when it comes, or the ending. A part may hold a choice card (an
    // event letter with answers, StorylineParts.cs) whose answer is the branch. Endings go to the chronicle and the
    // World page; a running storyline shows there with its parts so far (StorylinePanel.cs). One at a time, a second only
    // after the first has run a season; the first part is drawn by the storyteller like any other Rimfolk event. The
    // three of round 1: the factor's goods (StorylineFactor.cs), the beast coming down the valley (StorylineBeast.cs) and
    // the children from the burned village (StorylineChildren.cs). Saved in StoriesComponent (its storylines book).

    /// <summary>A storyline's own words: its name, how each part ended for the World page, what is on its way, its endings.</summary>
    public class StorylineDef : Def
    {
        /// <summary>Which storyline (StorylineMath.Kinds).</summary>
        public string kind;
        /// <summary>How a part ended, for the World page: "[kept] all kept under a roof, none opened".</summary>
        [MustTranslate] public List<string> outcomes = new List<string>();
        /// <summary>What is on its way, for the World page: tagged with the next part's key or "end:key".</summary>
        [MustTranslate] public List<string> coming = new List<string>();
        /// <summary>Its people gone, at war or destroyed: the plain ending's words (tagged with the part it stopped at).</summary>
        public List<StorylineEnding> endings = new List<StorylineEnding>();
        /// <summary>Outcomes shown in the World page's good colour, and in its bad one.</summary>
        public List<string> good = new List<string>();
        public List<string> bad = new List<string>();

        public StorylineEnding Ending(string key) => endings?.FirstOrDefault(e => e != null && e.key == key);

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            if (!StorylineMath.Kinds.Contains(kind)) yield return "storyline kind '" + kind + "' is not one of " + string.Join(", ", StorylineMath.Kinds);
            foreach (var key in StorylineMath.Endings(kind))
                if (Ending(key) == null)
                    yield return "storyline " + kind + " has no words for its ending '" + key + "'";
        }
    }

    /// <summary>One ending's words: its letter's title and text, and the line the chronicle and the World page keep.</summary>
    public class StorylineEnding
    {
        public string key;
        [MustTranslate] public List<string> titles = new List<string>();
        [MustTranslate] public List<string> texts = new List<string>();
        [MustTranslate] public List<string> summaries = new List<string>();
        /// <summary>good, bad or neutral: the letter's kind and the World page's colour.</summary>
        public string tone = "neutral";
    }

    /// <summary>One part of a storyline, as offered and as it ended.</summary>
    public class StorylineStep : IExposable
    {
        public string part;
        /// <summary>Its title in the Quests tab ("Part 2: The rival's offer").</summary>
        public string title;
        /// <summary>How it ended (the storyline's outcome: kept, lost, sold, declined...), null while it runs.</summary>
        public string outcome;
        public int offered = -1;
        public int ended = -1;

        public void ExposeData()
        {
            Scribe_Values.Look(ref part, "part");
            Scribe_Values.Look(ref title, "title");
            Scribe_Values.Look(ref outcome, "outcome");
            Scribe_Values.Look(ref offered, "offered", -1);
            Scribe_Values.Look(ref ended, "ended", -1);
        }
    }

    /// <summary>A storyline running, or ended, with the people, places, sums and things it is about.</summary>
    public class Storyline : IExposable
    {
        public int id;
        public string kind;
        public int started;
        /// <summary>How it ended, or null while it runs.</summary>
        public string ending;
        public int endedTick = -1;
        /// <summary>The parts so far.</summary>
        public List<StorylineStep> steps = new List<StorylineStep>();
        /// <summary>The mission of the part offered or under way, or -1.</summary>
        public int missionId = -1;
        /// <summary>What comes next: a part's key, or "end:key"; null while a part runs.</summary>
        public string next;
        public int due = -1;
        /// <summary>Hours the next part has waited for its moment.</summary>
        public int tries;
        /// <summary>Waiting a season or more for its next part: not counted against the pace.</summary>
        public bool quiet;
        /// <summary>What has happened so far, for the words ("ready", "told", "foundout", "back").</summary>
        public List<string> flags = new List<string>();
        /// <summary>The people who asked, and another people it involves (the rival's, the neighbours who take the children in).</summary>
        public Faction giver, other;
        /// <summary>The giver's own person (the factor, the hunter, the grown-up with the children), and a second (the rival, the beast).</summary>
        public Pawn x, y;
        /// <summary>Others it keeps in the world (the children, the porters).</summary>
        public List<Pawn> group = new List<Pawn>();
        /// <summary>Names kept for the words even when their people are gone: the giver's person, the second one.</summary>
        public string xName, yName;
        /// <summary>A name it gives something (the beast), the giver's town, a word for the texts (the beast's kind), which way (the tracks).</summary>
        public string name, place, tale, way;
        /// <summary>The crates' goods ("def:count|def:count") and their trust record (Peoples/Events/PeopleTrust.cs).</summary>
        public string load;
        public int trust = -1;
        public int count, worth, fee, amount, lost;
        /// <summary>The home it is told at (its uniqueID).</summary>
        public int mapId = -1;
        /// <summary>Hours each crate stood in the rain ("thingID:hours", the factor's); the graves laid out for the dead ("x,z", the sickness's).</summary>
        public List<string> wet = new List<string>();
        /// <summary>A def it is about (the beast's kind), a spot on the map (where the beast's kill lies), and a thing there (the kill).</summary>
        public string what;
        public IntVec3 spot = IntVec3.Invalid;
        public Thing thing;
        /// <summary>Two goals a part counts toward (traps and lengths of wall), and where each stood when it began.</summary>
        public int goalA, goalB, baseA, baseB;
        /// <summary>A place on the world map it made (the relic thief's camp: its world object's ID), or -1.</summary>
        public int site = -1;

        public bool Running => ending == null;

        public bool Has(string flag) => flags.Contains(flag);

        public void Set(string flag)
        {
            if (!flag.NullOrEmpty() && !flags.Contains(flag)) flags.Add(flag);
        }

        public StorylineStep Last => steps.Count > 0 ? steps[steps.Count - 1] : null;

        /// <summary>How many times a part has been offered (the beast that comes back).</summary>
        public int Times(string part) => steps.Count(s => s.part == part);

        public Map Map
        {
            get
            {
                if (mapId >= 0)
                    foreach (var m in Find.Maps)
                        if (m.uniqueID == mapId && m.IsPlayerHome) return m;
                return HappeningTracker.HomeMap();
            }
        }

        public void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                // Nobody gone from the game is saved as one of its people (a beast that left the map, a kill rotted away).
                if (x != null && x.Discarded) x = null;
                if (y != null && y.Discarded) y = null;
                group.RemoveAll(p => p == null || p.Discarded);
                if (thing != null && thing.Destroyed) thing = null;
            }
            Scribe_Values.Look(ref id, "id");
            Scribe_Values.Look(ref kind, "kind");
            Scribe_Values.Look(ref started, "started");
            Scribe_Values.Look(ref ending, "ending");
            Scribe_Values.Look(ref endedTick, "endedTick", -1);
            Scribe_Collections.Look(ref steps, "steps", LookMode.Deep);
            Scribe_Values.Look(ref missionId, "missionId", -1);
            Scribe_Values.Look(ref next, "next");
            Scribe_Values.Look(ref due, "due", -1);
            Scribe_Values.Look(ref tries, "tries");
            Scribe_Values.Look(ref quiet, "quiet");
            Scribe_Collections.Look(ref flags, "flags", LookMode.Value);
            Scribe_References.Look(ref giver, "giver");
            Scribe_References.Look(ref other, "other");
            Scribe_References.Look(ref x, "x");
            Scribe_References.Look(ref y, "y");
            Scribe_Collections.Look(ref group, "group", LookMode.Reference);
            Scribe_Values.Look(ref xName, "xName");
            Scribe_Values.Look(ref yName, "yName");
            Scribe_Values.Look(ref name, "name");
            Scribe_Values.Look(ref place, "place");
            Scribe_Values.Look(ref tale, "tale");
            Scribe_Values.Look(ref way, "way");
            Scribe_Values.Look(ref load, "load");
            Scribe_Values.Look(ref trust, "trust", -1);
            Scribe_Values.Look(ref count, "count");
            Scribe_Values.Look(ref worth, "worth");
            Scribe_Values.Look(ref fee, "fee");
            Scribe_Values.Look(ref amount, "amount");
            Scribe_Values.Look(ref lost, "lost");
            Scribe_Values.Look(ref mapId, "mapId", -1);
            Scribe_Collections.Look(ref wet, "wet", LookMode.Value);
            Scribe_Values.Look(ref what, "what");
            Scribe_Values.Look(ref spot, "spot", IntVec3.Invalid);
            Scribe_References.Look(ref thing, "thing");
            Scribe_Values.Look(ref goalA, "goalA");
            Scribe_Values.Look(ref goalB, "goalB");
            Scribe_Values.Look(ref baseA, "baseA");
            Scribe_Values.Look(ref baseB, "baseB");
            Scribe_Values.Look(ref site, "site", -1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                steps ??= new List<StorylineStep>();
                steps.RemoveAll(s => s == null);
                flags ??= new List<string>();
                group ??= new List<Pawn>();
                group.RemoveAll(p => p == null);
                wet ??= new List<string>();
            }
        }
    }

    /// <summary>
    /// What an ending leaves behind for a while: the factor's share of the trade brought every season for a year
    /// ("share"), no traders from her people for a year ("notrade"), or the pilgrims' relic at the colony's shrine for a
    /// year ("relic").
    /// </summary>
    public class StorylineAfter : IExposable
    {
        public string kind;
        public Faction faction;
        /// <summary>Who brings the share (the factor), kept in the world while it lasts.</summary>
        public Pawn pawn;
        public string who;
        public int next = -1;
        public int left;
        public int amount;
        public int until = -1;
        /// <summary>A thing it is about (the relic at the shrine).</summary>
        public Thing thing;

        public bool Over(int now) => kind == "share" ? left <= 0 : now > until;

        public void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving && pawn != null && pawn.Discarded) pawn = null;
            if (Scribe.mode == LoadSaveMode.Saving && thing != null && thing.Destroyed) thing = null;
            Scribe_Values.Look(ref kind, "kind");
            Scribe_References.Look(ref faction, "faction");
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Values.Look(ref who, "who");
            Scribe_Values.Look(ref next, "next", -1);
            Scribe_Values.Look(ref left, "left");
            Scribe_Values.Look(ref amount, "amount");
            Scribe_Values.Look(ref until, "until", -1);
            Scribe_References.Look(ref thing, "thing");
        }
    }

    /// <summary>The colony's storylines, and what their endings leave behind. Saved in StoriesComponent.</summary>
    public class StorylineBook : IExposable
    {
        public List<Storyline> lines = new List<Storyline>();
        public List<StorylineAfter> afters = new List<StorylineAfter>();

        /// <summary>Ended storylines are kept this long (the World page shows them for the first days, the pace reads when each kind last ended), then only in the chronicle.</summary>
        public const int KeepEndedDays = 60;

        public void ExposeData()
        {
            Scribe_Collections.Look(ref lines, "lines", LookMode.Deep);
            Scribe_Collections.Look(ref afters, "afters", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                lines ??= new List<Storyline>();
                lines.RemoveAll(s => s == null || s.kind == null);
                afters ??= new List<StorylineAfter>();
                afters.RemoveAll(a => a == null || a.kind == null);
            }
        }
    }

    /// <summary>
    /// What one storyline does beyond its parts: who may ask, its endings on the map, its people gone. One class each (found
    /// by the frame: every StorylineRules in the mod, by its <see cref="Kind"/>), beside its pure rules (Flows/).
    /// </summary>
    public abstract class StorylineRules
    {
        /// <summary>Which storyline (its StorylineFlow's kind).</summary>
        public abstract string Kind { get; }

        /// <summary>A people that could ask now (at peace with the colony and of the right kind), or null.</summary>
        public abstract Faction Giver(Map map);

        /// <summary>
        /// The ending plays out (people come, things change hands) before its letter. False: not now (a fight on the map),
        /// tried again in a few hours.
        /// </summary>
        public virtual bool Ending(Storyline s, string key, Map map, HappeningContext ctx) => true;

        /// <summary>Every hour while it runs (between its parts too).</summary>
        public virtual void Hourly(Storyline s, Map map, int now)
        {
        }

        /// <summary>Its people are gone or at war: what it leaves (crates opened, guests sent on).</summary>
        public virtual void Gone(Storyline s, Map map)
        {
        }

        /// <summary>Its people are no longer needed for it to go on (the beast came, whoever asked).</summary>
        public virtual bool NeedsGiver(Storyline s) => true;

        /// <summary>
        /// Every hour, what one of its endings left behind (a StorylineAfter, by its kind: the relic at the shrine): true when
        /// it was this storyline's to see to.
        /// </summary>
        public virtual bool After(StorylineAfter a, int now) => false;

        /// <summary>Dev mode: a part ended with <paramref name="outcome"/> by hand; what the branch reads is set as the part would have (a flag, the neighbours).</summary>
        public virtual void DebugEnded(Storyline s, string part, string outcome)
        {
        }
    }

    /// <summary>Starting storylines, the next part when it's due, their endings, and the people they keep.</summary>
    public static class Storylines
    {
        /// <summary>The storyline whose part is being offered right now (the part reads it).</summary>
        internal static Storyline current;

        /// <summary>A storyline being started by its first part (registered once the part is offered).</summary>
        internal static Storyline making;

        /// <summary>Dev mode: start a storyline whatever the pace says.</summary>
        internal static bool debugForce;

        private static Dictionary<string, StorylineRules> rules;

        /// <summary>Every storyline's rules beyond its parts, found once (each StorylineRules class in the mod).</summary>
        private static Dictionary<string, StorylineRules> AllRules
        {
            get
            {
                if (rules != null) return rules;
                rules = new Dictionary<string, StorylineRules>();
                foreach (var t in GenTypes.AllSubclassesNonAbstract(typeof(StorylineRules)))
                {
                    try
                    {
                        if (Activator.CreateInstance(t) is StorylineRules r && !r.Kind.NullOrEmpty() && !rules.ContainsKey(r.Kind)) rules[r.Kind] = r;
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("storyline rules " + t.Name, e);
                    }
                }
                return rules;
            }
        }

        private static int Now => Find.TickManager.TicksGame;

        private static CouncilSettings S => ColonyCouncilMod.Settings;

        public static StorylineRules Rules(string kind) => kind != null && AllRules.TryGetValue(kind, out var r) ? r : null;

        public static StorylineBook Book => StoriesComponent.Instance?.storylines;

        public static List<Storyline> All => Book?.lines ?? new List<Storyline>();

        public static IEnumerable<Storyline> Running => All.Where(s => s.Running);

        public static Storyline ById(int id) => All.FirstOrDefault(s => s.id == id);

        public static Storyline OfMission(int missionId) => missionId <= 0 ? null : All.FirstOrDefault(s => s.Running && s.missionId == missionId);

        /// <summary>A storyline's card letters carry "storyline:id" in their note.</summary>
        public static string Note(Storyline s) => "storyline:" + s.id;

        public static Storyline Of(HappeningContext ctx)
        {
            string n = ctx?.note;
            if (n == null || !n.StartsWith("storyline:") || !int.TryParse(n.Substring(10), out int id)) return null;
            return ById(id);
        }

        public static StorylineDef Def(string kind) => DefDatabase<StorylineDef>.AllDefsListForReading.FirstOrDefault(d => d.kind == kind);

        public static string Label(Storyline s) => Def(s?.kind)?.LabelCap.ToString() ?? s?.kind ?? "?";

        /// <summary>The event that offers a part of a storyline.</summary>
        public static IncidentDef PartDef(string kind, string part) =>
            DefDatabase<IncidentDef>.AllDefsListForReading.FirstOrDefault(d => d.GetModExtension<StorylinePart>() is StorylinePart p && p.story == kind && p.part == part);

        // ---------------- Starting ----------------

        /// <summary>The pace (StorylineMath.CanStart): one at a time, a second only after the first has run a season.</summary>
        public static bool CanStart(string kind)
        {
            if (debugForce) return !Running.Any(s => s.kind == kind);
            var runs = All.Select(s => new StorylineMath.Run { kind = s.kind, started = s.started, running = s.Running, quiet = s.quiet, ended = s.endedTick });
            return StorylineMath.CanStart(kind, Now, runs);
        }

        /// <summary>A new storyline, registered once its first part is offered.</summary>
        public static void Register(Storyline s, Map map)
        {
            var comp = StoriesComponent.Instance;
            if (comp == null || s == null) return;
            s.id = comp.NextId();
            s.started = Now;
            if (map != null) s.mapId = map.uniqueID;
            comp.storylines.lines.Add(s);
            Pin(s);
        }

        /// <summary>A part was offered: the storyline now waits on its mission.</summary>
        public static void PartOffered(Storyline s, Mission m, string part)
        {
            s.missionId = m.id;
            s.steps.Add(new StorylineStep { part = part, offered = Now });
            s.next = null;
            s.due = -1;
            s.tries = 0;
            s.quiet = false;
        }

        // ---------------- A part ends ----------------

        /// <summary>
        /// Missions.Finish: a mission ended. If it was a storyline's part, the storyline's rules pick what comes next (or
        /// the ending), and the storyline keeps how the part ended.
        /// </summary>
        public static void MissionEnded(Mission m, MissionState state, string outcome)
        {
            var s = m == null ? null : OfMission(m.id);
            if (s == null) return;
            s.missionId = -1;
            var step = s.Last;
            string result = StorylineMath.OutcomeOf(outcome, state == MissionState.Declined);
            if (step != null)
            {
                step.outcome = result;
                step.ended = Now;
                if (step.title.NullOrEmpty()) step.title = m.title;
            }
            Advance(s, step?.part, result);
        }

        /// <summary>What follows a part's outcome, by the storyline's own rules.</summary>
        public static void Advance(Storyline s, string part, string result)
        {
            if (s == null || !s.Running) return;
            var next = StorylineMath.Next(s.kind, part, result, s.Times(part), Rand.Value);
            if (next.flag != null) s.Set(next.flag);
            s.next = next.IsEnd ? "end:" + next.ending : next.part;
            s.due = Now + Mathf.RoundToInt(Rand.Range(next.min, Mathf.Max(next.min, next.max)) * GenDate.TicksPerDay);
            s.quiet = next.quiet;
            s.tries = 0;
            // An ending with no wait is told now, while the part's own letter is fresh.
            if (next.IsEnd && next.max <= 0f) Step(s, Now);
        }

        // ---------------- Every hour ----------------

        public static void Hourly(int now)
        {
            var book = Book;
            if (book == null) return;
            foreach (var s in book.lines.Where(s => s.Running).ToList())
            {
                try
                {
                    Step(s, now);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("storyline " + s.kind, e);
                    try
                    {
                        End(s, StorylineMath.Faded, null);
                    }
                    catch (Exception e2)
                    {
                        CouncilLog.ErrorOnce("storyline end " + s.kind, e2);
                        s.ending = StorylineMath.Faded;
                        s.endedTick = now;
                    }
                }
            }
            foreach (var a in book.afters.ToList())
            {
                try
                {
                    After(a, now);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("storyline after " + a.kind, e);
                    a.left = 0;
                    a.until = now - 1;
                }
            }
            book.afters.RemoveAll(a => a.Over(now) || a.faction == null || a.faction.defeated);
            book.lines.RemoveAll(s => !s.Running && now - s.endedTick > StorylineBook.KeepEndedDays * GenDate.TicksPerDay);
        }

        public static void Step(Storyline s, int now)
        {
            if (!s.Running) return;
            // Kept in the world while it needs them (a visit to the map lets the game's keep go).
            Pin(s);
            var map = s.Map;
            var r = Rules(s.kind);
            // Its people are gone, at war with the colony or destroyed: a plain ending, never stuck.
            if (r != null && r.NeedsGiver(s) && GiverGone(s))
            {
                s.Set(s.giver == null || s.giver.defeated ? "destroyed" : "war");
                if (s.missionId >= 0)
                {
                    // The part's own end (cancelled) brings this back here with the outcome "gone".
                    var m = Missions.ById(s.missionId);
                    if (m != null && m.Open) Missions.Finish(m, m.state == MissionState.Active ? MissionState.Failed : MissionState.Declined, "cancelled", null);
                    else s.missionId = -1;
                }
                if (s.Running && s.missionId < 0 && s.next != "end:" + StorylineMath.Gone) End(s, StorylineMath.Gone, map);
                return;
            }
            if (s.missionId >= 0)
            {
                // The part's mission is gone without telling (an older save, a broken def): it ended.
                var m = Missions.ById(s.missionId);
                if (m == null) MissionLost(s);
                else if (!m.Open) MissionEnded(m, m.state, m.outcome);
                else if (map != null) r?.Hourly(s, map, now);
                return;
            }
            if (map != null) r?.Hourly(s, map, now);
            if (s.next == null)
            {
                End(s, StorylineMath.Faded, map);
                return;
            }
            if (now < s.due) return;
            // Rimfolk's events switched off, or no home right now: it waits, and fades after a while.
            if (map == null || S == null || !S.worldEvents)
            {
                Wait(s, now, 6);
                return;
            }
            if (s.next.StartsWith("end:"))
            {
                End(s, s.next.Substring(4), map);
                return;
            }
            Offer(s, map, now);
        }

        private static void MissionLost(Storyline s)
        {
            s.missionId = -1;
            var step = s.Last;
            if (step != null && step.outcome == null)
            {
                step.outcome = StorylineMath.Ended;
                step.ended = Now;
            }
            Advance(s, step?.part, StorylineMath.Ended);
        }

        /// <summary>Not now: tried again in a few hours; after <see cref="StorylineMath.GiveUpDays"/> of waiting it fades.</summary>
        private static void Wait(Storyline s, int now, int hours)
        {
            s.due = now + GenDate.TicksPerHour * hours;
            s.tries += hours;
            if (s.tries > StorylineMath.GiveUpDays * 24f) End(s, StorylineMath.Faded, s.Map);
        }

        /// <summary>The next part offered now: its event fired, reading this storyline. Not now while another mission is open, a question waits or a fight is on.</summary>
        private static void Offer(Storyline s, Map map, int now)
        {
            var def = PartDef(s.kind, s.next);
            if (def == null)
            {
                End(s, StorylineMath.Faded, map);
                return;
            }
            if (Missions.All.Any(m => m.Open) || HappeningTracker.WaitingAt(map) || MapFacts.ThreatOn(map))
            {
                Wait(s, now, 4);
                return;
            }
            bool told = false;
            current = s;
            try
            {
                var parms = StorytellerUtility.DefaultParmsNow(def.category ?? IncidentCategoryDefOf.Misc, map);
                parms.forced = true;
                if (def.Worker is IStorylinePartWorker w && w.CanOffer(parms)) told = def.Worker.TryExecute(parms);
            }
            finally
            {
                current = null;
            }
            if (!told && s.Running) Wait(s, now, 6);
        }

        // ---------------- Endings ----------------

        /// <summary>
        /// The storyline ends: what the ending does on the map (Rules.Ending), its letter when it has one of its own, the
        /// chronicle, and the people it kept let go. A fight on the map puts off an ending that brings people in.
        /// </summary>
        public static void End(Storyline s, string key, Map map)
        {
            if (s == null || !s.Running) return;
            map ??= s.Map;
            var r = Rules(s.kind);
            var ctx = Context(s, map);
            if (key == StorylineMath.Gone)
            {
                try
                {
                    if (map != null) r?.Gone(s, map);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("storyline gone " + s.kind, e);
                }
            }
            else if (map != null && r != null)
            {
                bool done;
                try
                {
                    done = r.Ending(s, key, map, ctx);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("storyline ending " + s.kind + " " + key, e);
                    done = true;
                }
                if (!done)
                {
                    s.next = "end:" + key;
                    Wait(s, Now, 3);
                    return;
                }
            }
            s.ending = key;
            s.endedTick = Now;
            s.next = null;
            s.due = -1;
            s.missionId = -1;
            Tell(s, key, ctx);
            Release(s);
        }

        /// <summary>The ending's letter (when it has one of its own) and its line in the chronicle.</summary>
        private static void Tell(Storyline s, string key, HappeningContext ctx)
        {
            var def = Def(s.kind);
            var e = def?.Ending(key);
            if (e == null) return;
            var tags = Tags(s);
            tags.Add(key);
            string summary = Line(e.summaries, ctx, s, tags);
            if (StorylineMath.EndingHasLetter(s.kind, key))
            {
                string body = Line(e.texts, ctx, s, tags);
                string title = Line(e.titles, ctx, s, tags) ?? def.LabelCap.ToString();
                if (!body.NullOrEmpty())
                {
                    var letter = e.tone == "good" ? LetterDefOf.PositiveEvent : e.tone == "bad" ? LetterDefOf.NegativeEvent : LetterDefOf.NeutralEvent;
                    var look = s.x != null && s.x.Spawned ? new LookTargets(s.x) : LookTargets.Invalid;
                    ColonyCouncil.Tell.Letter(NewsKind.World, Importance.Notable, ctx.map, title, body, letter, look);
                }
            }
            if (summary.NullOrEmpty()) return;
            var who = ctx.a != null && !ctx.a.Dead && ctx.a.IsColonist ? ctx.a : null;
            CouncilGameComponent.Instance?.AddChronicle(new ChronicleEntry(ChronicleKind.World, def.LabelCap + ": " + summary, who));
        }

        /// <summary>The words for how it ended, for the World page.</summary>
        public static string EndingLine(Storyline s)
        {
            var e = Def(s?.kind)?.Ending(s.ending);
            if (e == null) return null;
            var tags = Tags(s);
            tags.Add(s.ending);
            return Stable(e.summaries, tags, s.id) is string line ? Fill(line, s, null) : null;
        }

        // ---------------- Its people ----------------

        /// <summary>The people who asked are gone (destroyed), hidden or at war with the colony.</summary>
        public static bool GiverGone(Storyline s) =>
            StorylineMath.GiverGone(s.giver != null, s.giver?.defeated ?? true, s.giver != null && (s.giver.HostileTo(Faction.OfPlayer) || s.giver.Hidden));

        public static bool Holds(Pawn p) => p != null && Running.Any(s => s.x == p || s.y == p || s.group.Contains(p));

        /// <summary>Kept in the world while the storyline needs them (they'd be forgotten once they leave the map).</summary>
        public static void Pin(Storyline s)
        {
            foreach (var p in People(s)) Pin(p);
        }

        public static void Pin(Pawn p)
        {
            if (p == null || p.Discarded || p.Faction == Faction.OfPlayer) return;
            if (p.Dead)
            {
                Find.WorldPawns.ForcefullyKeptPawns.Remove(p);
                return;
            }
            // A beast isn't one of the world's people: only the game's own keep holds it while it is away.
            if (!p.RaceProps.Humanlike)
            {
                if (!p.Spawned && Find.WorldPawns.Contains(p)) Find.WorldPawns.ForcefullyKeptPawns.Add(p);
                return;
            }
            WorldLedger.Keep(p, KeepFor.Storyline);
        }

        private static IEnumerable<Pawn> People(Storyline s)
        {
            if (s.x != null) yield return s.x;
            if (s.y != null) yield return s.y;
            foreach (var p in s.group)
                if (p != null)
                    yield return p;
        }

        private static void Release(Storyline s)
        {
            var people = People(s).ToList();
            s.x = s.y = null;
            s.group.Clear();
            foreach (var p in people) Release(p);
        }

        public static void Release(Pawn p)
        {
            if (p == null || Holds(p) || Book?.afters.Any(a => a.pawn == p) == true) return;
            if (!p.RaceProps.Humanlike)
            {
                Find.WorldPawns.ForcefullyKeptPawns.Remove(p);
                return;
            }
            if (!WorldLedger.Release(p, KeepFor.Storyline)) return;
            Find.WorldPawns.ForcefullyKeptPawns.Remove(p);
        }

        // ---------------- What endings leave behind ----------------

        public static void AddAfter(StorylineAfter a)
        {
            var book = Book;
            if (book == null || a == null) return;
            book.afters.Add(a);
            if (a.pawn != null) WorldLedger.Keep(a.pawn, KeepFor.Storyline);
        }

        /// <summary>No traders of this people come (the factor's people after a debt unpaid).</summary>
        public static bool NoTrade(Faction f)
        {
            var book = Book;
            int now = Now;
            return f != null && book != null && book.afters.Any(a => a.kind == "notrade" && a.faction == f && now <= a.until);
        }

        private static void After(StorylineAfter a, int now)
        {
            // What another storyline's ending left (the relic at the shrine): its own rules see to it.
            if (a.kind != "share")
            {
                foreach (var r in AllRules.Values)
                    if (r.After(a, now))
                        return;
                return;
            }
            if (a.left <= 0 || now < a.next) return;
            var map = HappeningTracker.HomeMap();
            if (map == null || a.faction == null || a.faction.defeated || a.faction.HostileTo(Faction.OfPlayer))
            {
                a.left = 0;
                return;
            }
            // Not into a fight: she waits on the road a while.
            if (MapFacts.ThreatOn(map))
            {
                a.next = now + GenDate.TicksPerHour * 6;
                return;
            }
            FactorRules.Share(a, map);
            a.left--;
            a.next = now + Mathf.RoundToInt(StorylineMath.SeasonDays * GenDate.TicksPerDay);
            if (a.left <= 0 && a.pawn != null)
            {
                var p = a.pawn;
                a.pawn = null;
                Release(p);
            }
        }

        // ---------------- Words ----------------

        /// <summary>The tags its words are picked by: what has happened (its flags, each part's outcome) and who asked.</summary>
        public static List<string> Tags(Storyline s)
        {
            var tags = new List<string>(s.flags);
            foreach (var st in s.steps)
                if (st.outcome != null)
                    tags.Add(st.part + ":" + st.outcome);
            var f = s.giver;
            if (f != null && !f.IsPlayer)
            {
                tags.Add("their:" + Ages.Tag(Ages.Of(f)));
                tags.Add(Peoples.TechOf(f) <= TechLevel.Medieval ? "their:lowtech" : "their:hightech");
                if (Ages.IsEmpire(f)) tags.Add("their:empire");
                Peoples.AddTags(f, tags);
            }
            if (s.x != null && s.x.gender == Gender.Female) tags.Add("giver:female");
            return tags;
        }

        /// <summary>The storyline's people, places and sums for its words.</summary>
        public static HappeningContext Context(Storyline s, Map map)
        {
            var ctx = new HappeningContext
            {
                map = map, faction = s.giver, enemy = s.other, stranger = s.x != null && !s.x.Discarded ? s.x : null, placeName = s.place, beast = s.name,
                tale = s.tale, count = s.count, note = Note(s)
            };
            if (map != null) ctx.a = VarietyUtil.Best(map, SkillDefOf.Social) ?? StorytellerEvents.Adults(map).RandomElementWithFallback();
            return ctx;
        }

        /// <summary>The storyline's own names in a line: {GIVER}, {RIVAL}, {OTHERPEOPLE}, {NAME}, {KIND}, {WAY}, {SILVER}, {TRAPS}, {WALLS}; the rest the context's.</summary>
        public static string Fill(string line, Storyline s, HappeningContext ctx, string silver = null)
        {
            if (line.NullOrEmpty()) return line;
            line = Texts.Fill(line, ("GIVER", s?.xName ?? "?"), ("RIVAL", s?.yName ?? "?"), ("OTHERPEOPLE", s?.other?.Name ?? "?"), ("NAME", s?.name ?? "?"),
                ("KIND", s?.tale ?? "?"), ("WAY", s?.way ?? "?"), ("SILVER", silver ?? (s?.amount ?? 0).ToString()), ("TRAPS", (s?.goalA ?? 0).ToString()),
                ("WALLS", (s?.goalB ?? 0).ToString()));
            return ctx != null ? ctx.Format(line) : line;
        }

        /// <summary>
        /// A line whose tags fit (never one tagged for another branch: no line at all rather than a wrong one), with {ITEM},
        /// {DAYS} and the storyline's names filled in.
        /// </summary>
        public static string Line(List<string> lines, HappeningContext ctx, Storyline s, IEnumerable<string> tags, string item = null, string days = null, string silver = null)
        {
            if (lines.NullOrEmpty()) return null;
            var context = new HashSet<string>();
            Flavor.AddEraTags(context);
            var map = ctx?.map;
            if (map != null)
            {
                switch (GenLocalDate.Season(map))
                {
                    case Season.Spring: context.Add("spring"); break;
                    case Season.Summer:
                    case Season.PermanentSummer: context.Add("summer"); break;
                    case Season.Fall: context.Add("fall"); break;
                    case Season.Winter:
                    case Season.PermanentWinter: context.Add("winter"); break;
                }
            }
            if (tags != null)
                foreach (var t in tags)
                    if (!t.NullOrEmpty())
                        context.Add(t);
            string pick = Flavor.Pick(lines, context);
            if (pick.NullOrEmpty()) return null;
            if (item != null) pick = Texts.Fill(pick, ("ITEM", item));
            if (days != null) pick = Texts.Fill(pick, ("DAYS", days));
            return Fill(pick, s, ctx, silver);
        }

        /// <summary>The same fitting line for as long as <paramref name="seed"/> stays the same (the World page is drawn every frame).</summary>
        public static string Stable(List<string> lines, IEnumerable<string> tags, int seed)
        {
            if (lines.NullOrEmpty()) return null;
            var context = new HashSet<string>(tags.Where(t => !t.NullOrEmpty()));
            Flavor.AddEraTags(context);
            var fit = lines.Where(l => !l.NullOrEmpty() && Flavor.TechFits(l) && Flavor.Match(l, context) >= 0).ToList();
            if (fit.Count == 0) return null;
            // The most particular line first (the most tags that speak to it), then by the seed.
            int best = fit.Max(l => Flavor.Match(l, context));
            fit = fit.Where(l => Flavor.Match(l, context) == best).ToList();
            return Flavor.Strip(fit[(seed % fit.Count + fit.Count) % fit.Count]);
        }

        /// <summary>A part's outcome in words for the World page ("all kept under a roof").</summary>
        public static string OutcomeWords(Storyline s, StorylineStep step)
        {
            if (step?.outcome == null) return null;
            var tags = Tags(s);
            tags.Add(step.part);
            tags.Add("outcome:" + step.outcome);
            string line = Stable(Def(s.kind)?.outcomes, tags, s.id * 31 + s.steps.IndexOf(step));
            return line == null ? null : Fill(line, s, null);
        }

        /// <summary>What is on its way, for the World page.</summary>
        public static string ComingWords(Storyline s)
        {
            if (s.next == null) return null;
            var tags = Tags(s);
            tags.Add(s.next.StartsWith("end:") ? s.next : "next:" + s.next);
            string line = Stable(Def(s.kind)?.coming, tags, s.id * 17 + s.steps.Count);
            return line == null ? null : Fill(line, s, null);
        }

        /// <summary>One line for the dev log.</summary>
        public static string Describe(Storyline s)
        {
            var parts = s.steps.Select(st => st.part + "=" + (st.outcome ?? "open"));
            int now = Now;
            return "#" + s.id + " " + s.kind + (s.Running ? "" : " ENDED " + s.ending) + " by " + (s.giver?.Name ?? "nobody") + " (" + (s.xName ?? "?") + ")"
                   + "; parts: " + string.Join(", ", parts) + "; mission " + s.missionId + "; next " + (s.next ?? "-")
                   + (s.due > now ? " in " + ((s.due - now) / (float)GenDate.TicksPerDay).ToString("0.0") + " days" : "")
                   + (s.quiet ? " (quiet)" : "") + "; flags " + string.Join(" ", s.flags) + "; count " + s.count + ", worth " + s.worth + ", fee " + s.fee + ", amount " + s.amount
                   + ", lost " + s.lost;
        }
    }

    /// <summary>Its traders don't come for a year after the colony left a debt unpaid (StorylineFactor.cs, the factor's goods).</summary>
    [HarmonyPatch(typeof(IncidentWorker_TraderCaravanArrival), nameof(IncidentWorker_TraderCaravanArrival.FactionCanBeGroupSource))]
    public static class Patch_StorylineNoTrade
    {
        public static void Postfix(Faction f, ref bool __result)
        {
            if (!__result || !RimfolkPatches.On) return;
            try
            {
                if (Storylines.NoTrade(f)) __result = false;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("storylines: no traders for a year", e);
            }
        }
    }
}
