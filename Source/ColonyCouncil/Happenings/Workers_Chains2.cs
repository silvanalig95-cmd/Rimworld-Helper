using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // Story chains: longer stories told in stages over days and weeks, each stage a Rimfolk event. The first stage is
    // drawn by the storyteller like any other event; the colony's answer (and some luck, settled when it matters) decides
    // which stage comes next and when, and the chain keeps that in its own save data (StoryChain) until the stage is due.
    // Later stages have a base chance of 0: only a chain that has reached them can tell them. Every chain ends in one of
    // several endings, told in a letter and written in the chronicle. Five chains: the door in the hill, a fugitive's
    // debt, bad blood between two families of a neighbor town, a wandering preacher, and a lost expedition. Texts are in
    // Defs/IncidentDefs/Council_Events_Stories.xml; the chain-wide ones (endings, World page lines) on each first stage.

    /// <summary>
    /// The text of a story stage: which chain it belongs to and its own name (a tag in its texts, and on the World page).
    /// A chain's first stage also carries the chain-wide texts: its endings, the World page lines for each stage still to
    /// come, names for places, and the story of a fight it may bring.
    /// </summary>
    public class StoryText : HappeningLivingText
    {
        public string chain;
        public string key;
        public bool start;
        /// <summary>Endings, tagged with the ending's name (and the chain's flags): two short paragraphs each.</summary>
        [MustTranslate] public List<string> endings;
        [MustTranslate] public List<string> endingTitles;
        /// <summary>The chronicle's line for each ending (else the ending's first sentence).</summary>
        [MustTranslate] public List<string> summaries;
        /// <summary>Names the chain gives things (a ruin), with era tags.</summary>
        [MustTranslate] public List<string> names;
        /// <summary>Above the game's raid letter, when a stage brings a fight (tagged with the stage's name).</summary>
        [MustTranslate] public List<string> raidTitles;
        [MustTranslate] public List<string> raid;
        /// <summary>Endings told in a good letter, and in a bad one (the rest are neutral).</summary>
        public List<string> good;
        public List<string> bad;
    }

    /// <summary>One story in progress (or ended), with the people, places and sums it is about.</summary>
    public class StoryChain : IExposable
    {
        public int id;
        public string kind;
        /// <summary>How many stages have been told.</summary>
        public int stage;
        /// <summary>The stage to tell next (a def name), or "end:key" for an ending told at a set time. Null while a letter waits.</summary>
        public string next;
        public int due = -1;
        /// <summary>The ending to tell instead if the next stage can't happen (its people are gone).</summary>
        public string fallback;
        public int tries;
        /// <summary>A letter of this chain is waiting for an answer.</summary>
        public bool waiting;
        public int started;
        /// <summary>The colonist the story follows (A).</summary>
        public Pawn hero;
        /// <summary>A stranger it is about (X), kept in the world while the chain needs them.</summary>
        public Pawn x;
        /// <summary>A second person (B): another stranger, or a colonist.</summary>
        public Pawn y;
        public Faction faction;
        public Faction enemy;
        public string place;
        public string tale;
        public string dead;
        public string beast;
        public string relation;
        /// <summary>The sum in play ({COUNT}).</summary>
        public int amount;
        /// <summary>What a find is worth.</summary>
        public int value;
        public int extra;
        /// <summary>What has happened so far (tags for the texts): "studied", "hoard", "innocent"...</summary>
        public List<string> flags = new List<string>();
        /// <summary>How it ended ("faded": it simply stopped), or null while it runs.</summary>
        public string ending;
        public int endedTick = -1;

        public bool Running => ending == null;

        public bool Has(string flag) => flags.Contains(flag);

        public void Set(params string[] fs)
        {
            foreach (var f in fs)
                if (!f.NullOrEmpty() && !flags.Contains(f)) flags.Add(f);
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_Values.Look(ref kind, "kind");
            Scribe_Values.Look(ref stage, "stage");
            Scribe_Values.Look(ref next, "next");
            Scribe_Values.Look(ref due, "due", -1);
            Scribe_Values.Look(ref fallback, "fallback");
            Scribe_Values.Look(ref tries, "tries");
            Scribe_Values.Look(ref waiting, "waiting");
            Scribe_Values.Look(ref started, "started");
            Scribe_References.Look(ref hero, "hero");
            Scribe_References.Look(ref x, "x");
            Scribe_References.Look(ref y, "y");
            Scribe_References.Look(ref faction, "faction");
            Scribe_References.Look(ref enemy, "enemy");
            Scribe_Values.Look(ref place, "place");
            Scribe_Values.Look(ref tale, "tale");
            Scribe_Values.Look(ref dead, "dead");
            Scribe_Values.Look(ref beast, "beast");
            Scribe_Values.Look(ref relation, "relation");
            Scribe_Values.Look(ref amount, "amount");
            Scribe_Values.Look(ref value, "value");
            Scribe_Values.Look(ref extra, "extra");
            Scribe_Collections.Look(ref flags, "flags", LookMode.Value);
            Scribe_Values.Look(ref ending, "ending");
            Scribe_Values.Look(ref endedTick, "endedTick", -1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && flags == null) flags = new List<string>();
        }
    }

    /// <summary>A line for the council's World page, from a story or a mission.</summary>
    public class WorldNote
    {
        public string title;
        public string text;
        public string tip;
        public Texture2D icon;
        public Color color = Color.white;
        public Faction faction;
        public int due = -1;
        public Action click;
    }

    /// <summary>The running stories: starting them, telling the next stage when it's due, ending them, and the World page.</summary>
    public static class StoryChains
    {
        /// <summary>The chain whose stage is being told right now (stages read it when they fire).</summary>
        internal static StoryChain current;

        private static Dictionary<string, IncidentDef> homes;

        private static readonly Color Calm = new Color(0.72f, 0.8f, 0.95f);

        private static int Now => Find.TickManager.TicksGame;

        private static CouncilSettings S => ColonyCouncilMod.Settings;

        public static void ClearCache() => homes = null;

        public static List<StoryChain> All => StoriesComponent.Instance?.chains ?? new List<StoryChain>();

        public static string Note(StoryChain c) => "story:" + c.id;

        public static StoryChain ById(int id) => All.FirstOrDefault(c => c.id == id);

        /// <summary>The chain an event's letter belongs to.</summary>
        public static StoryChain Of(HappeningContext ctx)
        {
            string n = ctx?.note;
            if (n == null || !n.StartsWith("story:") || !int.TryParse(n.Substring(6), out int id)) return null;
            return ById(id);
        }

        /// <summary>A chain's first stage (its texts tell the endings).</summary>
        public static IncidentDef HomeDef(string kind)
        {
            if (kind == null) return null;
            if (homes == null)
            {
                homes = new Dictionary<string, IncidentDef>();
                foreach (var d in DefDatabase<IncidentDef>.AllDefsListForReading)
                {
                    var t = d.GetModExtension<StoryText>();
                    if (t != null && t.start && t.chain != null) homes[t.chain] = d;
                }
            }
            return homes.TryGetValue(kind, out var def) ? def : null;
        }

        public static StoryText HomeText(string kind) => HomeDef(kind)?.GetModExtension<StoryText>();

        public static HappeningWorker_Story Worker(string kind) => HomeDef(kind)?.Worker as HappeningWorker_Story;

        /// <summary>No more than two stories at once, never two of a kind, and a kind that just ended rests a while.</summary>
        public static bool CanStart(string kind)
        {
            var all = All;
            int now = Now;
            if (all.Count(c => c.Running) >= 2) return false;
            return !all.Any(c => c.kind == kind && (c.Running || now - c.endedTick < 60 * GenDate.TicksPerDay));
        }

        public static void Register(StoryChain c)
        {
            var comp = StoriesComponent.Instance;
            if (comp == null || c == null) return;
            c.id = comp.NextId();
            c.started = Now;
            comp.chains.Add(c);
        }

        /// <summary>What a stage settled goes back into the chain (people and places come and go by the workers' own hand).</summary>
        public static void Keep(StoryChain c, HappeningContext ctx)
        {
            if (ctx.a != null && ctx.a.IsColonist) c.hero = ctx.a;
            c.faction = ctx.faction;
            c.enemy = ctx.enemy;
            c.place = ctx.placeName;
            c.tale = ctx.tale;
            c.dead = ctx.dead;
            c.beast = ctx.beast;
            c.relation = ctx.relation;
            c.amount = ctx.count;
        }

        /// <summary>The chain's people, places and sums, for a stage's texts.</summary>
        public static void Fill(HappeningContext ctx, StoryChain c, Map map)
        {
            ctx.a = Hero(c, map);
            ctx.stranger = c.x;
            ctx.b = c.y;
            ctx.faction = c.faction;
            ctx.enemy = c.enemy;
            ctx.placeName = c.place;
            ctx.tale = c.tale;
            ctx.dead = c.dead;
            ctx.beast = c.beast;
            ctx.relation = c.relation;
            ctx.count = c.amount;
            ctx.note = Note(c);
        }

        public static HappeningContext Context(StoryChain c, IncidentDef def, Map map)
        {
            var ctx = new HappeningContext { incident = def, map = map };
            Fill(ctx, c, map);
            return ctx;
        }

        /// <summary>The colonist the story follows; if they're gone, whoever speaks for the colony now.</summary>
        public static Pawn Hero(StoryChain c, Map map)
        {
            var h = c.hero;
            if (h != null && !h.Dead && !h.Discarded && h.IsColonist) return h;
            if (map == null) return h != null && !h.Dead ? h : null;
            var p = VarietyUtil.Best(map, SkillDefOf.Social) ?? StorytellerEvents.Adults(map).RandomElementWithFallback();
            if (p != null) c.hero = p;
            return p;
        }

        /// <summary>What comes next, in a while: a stage (def name) or "end:key".</summary>
        public static void Next(StoryChain c, string stage, float minDays, float maxDays, string fallback = null)
        {
            c.next = stage;
            c.due = Now + Mathf.RoundToInt(Rand.Range(minDays, maxDays) * GenDate.TicksPerDay);
            c.fallback = fallback;
            c.tries = 0;
        }

        // ---------------- Endings ----------------

        /// <summary>
        /// The story ends: its ending in a letter and the chronicle (none for a null key: it just stops), and the people it
        /// kept in the world are let go. <paramref name="item"/> fills {ITEM}; <paramref name="tags"/> pick among the
        /// ending's texts.
        /// </summary>
        public static void End(StoryChain c, string key, HappeningContext ctx = null, string item = null, LookTargets look = null, params string[] tags)
        {
            if (c == null || !c.Running) return;
            var home = HomeDef(c.kind);
            var map = ctx?.map ?? HappeningTracker.HomeMap();
            if (key != null && home != null)
            {
                try
                {
                    Tell(c, key, home, ctx ?? Context(c, home, map), item, look, tags);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("story ending " + c.kind, e);
                }
            }
            c.ending = key ?? "faded";
            c.endedTick = Now;
            c.next = null;
            c.fallback = null;
            c.waiting = false;
            var x = c.x;
            var y = c.y;
            c.x = c.y = null;
            c.hero = null;
            Release(x);
            Release(y);
        }

        private static void Tell(StoryChain c, string key, IncidentDef home, HappeningContext ctx, string item, LookTargets look, string[] extra)
        {
            var text = home.GetModExtension<StoryText>();
            var tags = new List<string>(c.flags) { key };
            if (extra != null) tags.AddRange(extra);
            string body = Line(text?.endings, ctx, key, tags, item);
            if (body.NullOrEmpty()) return;
            string title = Line(text.endingTitles, ctx, key, tags, item) ?? home.LabelCap.ToString();
            var letter = text.good != null && text.good.Contains(key) ? LetterDefOf.PositiveEvent
                : text.bad != null && text.bad.Contains(key) ? LetterDefOf.NegativeEvent : LetterDefOf.NeutralEvent;
            Find.LetterStack.ReceiveLetter(title, body, letter, look ?? LookTargets.Invalid);
            string summary = Line(text.summaries, ctx, key, tags, item) ?? FirstSentence(body);
            var who = ctx.a != null && !ctx.a.Dead && ctx.a.IsColonist ? ctx.a : null;
            CouncilGameComponent.Instance?.AddChronicle(new ChronicleEntry(ChronicleKind.World, home.LabelCap + ": " + summary, who));
        }

        private static string FirstSentence(string text)
        {
            int cut = text.IndexOfAny(new[] { '.', '!', '?' });
            return cut > 0 && cut < 240 ? text.Substring(0, cut + 1) : text.Length > 240 ? text.Substring(0, 240) + "..." : text;
        }

        // ---------------- Every hour ----------------

        public static void Hourly(int now)
        {
            var comp = StoriesComponent.Instance;
            if (comp == null) return;
            foreach (var c in comp.chains.Where(c => c.Running).ToList())
            {
                try
                {
                    Step(c, now);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("story " + c.kind, e);
                    End(c, null);
                }
            }
            // Stories that ended long ago are only in the chronicle now.
            comp.chains.RemoveAll(c => !c.Running && now - c.endedTick > 120 * GenDate.TicksPerDay);
        }

        private static void Step(StoryChain c, int now)
        {
            Pin(c.x);
            Pin(c.y);
            if (c.waiting)
            {
                if (LetterOut(c)) return;
                // The letter went without an answer reaching the story.
                c.waiting = false;
            }
            if (c.next == null)
            {
                End(c, null);
                return;
            }
            if (now < c.due) return;
            var map = HappeningTracker.HomeMap();
            // Rimfolk events switched off, or no home right now: the story waits (a month at most).
            if (map == null || !S.worldEvents)
            {
                c.due = now + GenDate.TicksPerHour * 6;
                if (++c.tries > 120) End(c, null);
                return;
            }
            if (c.next.StartsWith("end:"))
            {
                string key = c.next.Substring(4);
                var w = Worker(c.kind);
                if (w != null) w.Ending(c, key, map);
                if (c.Running) End(c, key);
                return;
            }
            var def = DefDatabase<IncidentDef>.GetNamedSilentFail(c.next);
            if (def == null || !(def.Worker is HappeningWorker_Story worker))
            {
                End(c, null);
                return;
            }
            // Not on top of another question, nor in the middle of a fight.
            if (HappeningTracker.Waiting || MapFacts.ThreatOn(map))
            {
                c.due = now + GenDate.TicksPerHour * 3;
                return;
            }
            bool told = false;
            current = c;
            try
            {
                var parms = StorytellerUtility.DefaultParmsNow(def.category ?? IncidentCategoryDefOf.Misc, map);
                parms.forced = true;
                if (worker.CanTell(parms)) told = def.Worker.TryExecute(parms);
            }
            finally
            {
                current = null;
            }
            if (told || !c.Running) return;
            // Its people are gone or its neighbors at war: tried again for a few days, then the other ending (or none).
            c.due = now + GenDate.TicksPerHour * 12;
            c.tries += 20;
            if (c.tries > 120)
            {
                string fb = c.fallback;
                if (fb != null && (c.x == null || !c.x.Dead))
                {
                    c.next = "end:" + fb;
                    Worker(c.kind)?.Ending(c, fb, map);
                }
                if (c.Running) End(c, null);
            }
        }

        private static bool LetterOut(StoryChain c)
        {
            string note = Note(c);
            return Find.LetterStack.LettersListForReading.Any(l => l is ChoiceLetter_Happening h && !h.resolved && h.ctx?.note == note);
        }

        // ---------------- People kept in the world ----------------

        public static bool Holds(Pawn p) => p != null && All.Any(c => c.Running && (c.x == p || c.y == p));

        /// <summary>Kept in the world while a story needs them (they'd be forgotten otherwise once they leave the map).</summary>
        public static void Pin(Pawn p)
        {
            if (p == null || p.Discarded || p.Faction == Faction.OfPlayer) return;
            if (p.Dead)
            {
                Find.WorldPawns.ForcefullyKeptPawns.Remove(p);
                return;
            }
            Find.WorldPawns.ForcefullyKeptPawns.Add(p);
        }

        private static void Release(Pawn p)
        {
            if (p == null || Holds(p)) return;
            var t = CouncilGameComponent.Instance?.happenings;
            if (t != null && (t.Holds(p) || t.departed.Any(d => d.pawn == p) || t.raids.Any(r => r.leader == p))) return;
            if (CouncilGameComponent.Instance?.nemeses.IsNemesis(p) ?? false) return;
            if (Missions.Holds(p) || WorldSituations.Holds(p)) return;
            Find.WorldPawns.ForcefullyKeptPawns.Remove(p);
        }

        // ---------------- Text ----------------

        /// <summary>A line tagged with <paramref name="key"/> (among others), fitting the era, season and tags; {ITEM} filled in.</summary>
        public static string Line(List<string> lines, HappeningContext ctx, string key, IEnumerable<string> tags, string item = null, string days = null)
        {
            if (lines.NullOrEmpty() || ctx == null) return null;
            var keyed = key == null ? lines : lines.Where(l => HasTag(l, key)).ToList();
            if (keyed.Count == 0) return null;
            var all = new List<string>();
            if (tags != null) all.AddRange(tags);
            if (key != null) all.Add(key);
            return VarietyUtil.Line(keyed, ctx, all, item ?? "", days);
        }

        private static bool HasTag(string line, string key)
        {
            Flavor.Strip(line, out var t);
            return t != null && t.Any(x => x.Split('|').Any(a => a.Trim() == key));
        }

        /// <summary>The same line for as long as <paramref name="seed"/> stays the same (the World page is drawn every frame).</summary>
        private static string Stable(List<string> lines, string key, IEnumerable<string> tags, int seed)
        {
            if (lines.NullOrEmpty()) return null;
            var context = new HashSet<string>(tags) { key };
            Flavor.AddEraTags(context);
            var fit = lines.Where(l => !l.NullOrEmpty() && HasTag(l, key) && Flavor.TechFits(l) && Flavor.Match(l, context) >= 0).ToList();
            if (fit.Count == 0) return null;
            return Flavor.Strip(fit[(seed % fit.Count + fit.Count) % fit.Count]);
        }

        // ---------------- The World page ----------------

        /// <summary>The name of what a chain is waiting for: its next stage's key, or the ending's.</summary>
        private static string PendingKey(StoryChain c)
        {
            if (c.next == null) return null;
            if (c.next.StartsWith("end:")) return c.next.Substring(4);
            return DefDatabase<IncidentDef>.GetNamedSilentFail(c.next)?.GetModExtension<StoryText>()?.key;
        }

        /// <summary>Stories with something known to be on its way, with a countdown ("what's coming").</summary>
        public static List<WorldNote> Coming(int now) => Notes(now, true);

        /// <summary>Stories that may come back, without a date ("who's out there").</summary>
        public static List<WorldNote> OutThere(int now) => Notes(now, false);

        private static List<WorldNote> Notes(int now, bool coming)
        {
            var list = new List<WorldNote>();
            foreach (var c in All)
            {
                if (!c.Running || c.waiting) continue;
                // Built every second while the World page is open: a story that can't be told here is left out, logged once.
                try
                {
                    string key = PendingKey(c);
                    var home = HomeDef(c.kind);
                    var text = home?.GetModExtension<StoryText>();
                    if (key == null || text == null) continue;
                    string line = Stable(coming ? text.coming : text.remembered, key, c.flags, c.id * 31 + c.stage);
                    if (line.NullOrEmpty()) continue;
                    var ctx = Context(c, home, null);
                    list.Add(new WorldNote
                    {
                        title = coming ? home.LabelCap.ToString() : null,
                        text = ctx.Format(line.Replace("{DAYS}", TextUtil.DaysString(Mathf.Max(0, c.due - now)))),
                        icon = Icons.WorldEvent,
                        color = Calm,
                        faction = coming ? c.faction : null,
                        due = coming ? c.due : -1
                    });
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("story note " + c.kind, e);
                }
            }
            return list;
        }

        // ---------------- Small helpers for the stages ----------------

        public static int Silver(Map map, float min, float max) => StorytellerEvents.Silver(map, new FloatRange(min, max));

        /// <summary>Goodwill with a neighbor, never enough to turn them hostile, and never touching a war either way.</summary>
        public static void Goodwill(Faction f, int amount)
        {
            if (f == null || f.IsPlayer || f.defeated || !f.HasGoodwill || amount == 0 || f.HostileTo(Faction.OfPlayer)) return;
            int change = amount > 0 ? amount : Mathf.Min(0, Mathf.Max(amount, -60 - f.PlayerGoodwill));
            if (change != 0) f.TryAffectGoodwillWith(Faction.OfPlayer, change, true, false);
        }

        /// <summary>Things of the colony's era worth about <paramref name="value"/>, left near a spot; what they were, or null.</summary>
        public static string Gift(Map map, IntVec3 near, float value, bool fancy, bool pod = false)
        {
            if (map == null || value < 20f) return null;
            var things = Loot.Make(value, fancy);
            if (things.Count == 0) return null;
            Loot.Deliver(things, map, near.IsValid ? near : DropCellFinder.TradeDropSpot(map), pod);
            return Loot.Describe(things);
        }

        public static void Give(Pawn p, string thought, IncidentDef def, Pawn about = null) => StorytellerEvents.Give(p, thought, def, about);

        private static readonly string[] Compass = { "North", "NorthEast", "East", "SouthEast", "South", "SouthWest", "West", "NorthWest" };

        /// <summary>"to the north": somewhere off the colony's map.</summary>
        public static string Direction() => ("CC_St_Dir_" + Compass.RandomElement()).Translate();

        /// <summary>A family name ("Harrow"), or the person's short name.</summary>
        public static string Family(Pawn p) => p?.Name is NameTriple t && !t.Last.NullOrEmpty() ? t.Last : p?.LabelShort ?? "?";

        public static bool Alive(Pawn p) => p != null && !p.Dead && !p.Discarded;

        /// <summary>
        /// Someone of the story's still their own person: alive, not the colony's (recruited, a prisoner or a slave),
        /// nobody's captive, not with a caravan. Only such a stranger is brought back, recruited or turned against the colony.
        /// </summary>
        public static bool Free(Pawn p) =>
            Alive(p) && p.Faction != Faction.OfPlayer && !p.IsPrisoner && !p.IsSlave && !People.Kidnapped(p) && p.GetCaravan() == null;

        /// <summary>Someone out in the world who could come visiting now.</summary>
        public static bool CanVisit(Pawn p) =>
            Alive(p) && !p.Spawned && p.Faction != null && !p.Faction.IsPlayer && !p.Faction.defeated && !p.Faction.HostileTo(Faction.OfPlayer)
            && !p.IsPrisoner && !People.Kidnapped(p) && p.GetCaravan() == null && !QuestUtility.IsReservedByQuestOrQuestBeingGenerated(p);

        /// <summary>Visitors walk in and spend some hours at a spot; true if they came.</summary>
        public static bool Visit(IEnumerable<Pawn> pawns, Map map, Faction f, IntVec3? spot, float hours)
        {
            var list = pawns.Where(p => p != null && !p.Dead && !p.Spawned).ToList();
            if (list.Count == 0 || map == null || f == null) return false;
            return Arrivals.Visit(list, map, f, spot, hours) != null;
        }
    }

    // ===================== The base of every stage =====================

    /// <summary>
    /// A stage of a story chain. A first stage sets the story up (<see cref="Setup"/>) and starts the chain; a later one
    /// reads it (<see cref="Prepare"/>). Both may bring people in (<see cref="Arrive"/>), and the colony's answer decides
    /// what comes next (<see cref="Answer"/>). A stage without options goes on through <see cref="Told"/>.
    /// </summary>
    public abstract class HappeningWorker_Story : HappeningWorker_Visitors
    {
        private StoryChain making;
        private readonly List<Pawn> fresh = new List<Pawn>();

        public StoryText Story => def.GetModExtension<StoryText>();

        protected string Key => Story?.key ?? def.defName;

        protected bool IsStart => Story?.start ?? false;

        /// <summary>The event's own checks (settings, colony, and whether the story can go on), for a stage that's due.</summary>
        public bool CanTell(IncidentParms parms) => CanFireNowSub(parms);

        protected override bool CanHappen(Map map, HappeningExtension ext)
        {
            if (IsStart) return Story.chain != null && StoryChains.CanStart(Story.chain) && StorytellerEvents.Adults(map).Any() && CanBegin(map, ext);
            var c = StoryChains.current;
            return c != null && c.Running && c.next == def.defName && CanGoOn(c, map);
        }

        /// <summary>A first stage: what the story needs to be able to start.</summary>
        protected virtual bool CanBegin(Map map, HappeningExtension ext) => true;

        /// <summary>A later stage: whether its people and places are still there.</summary>
        protected virtual bool CanGoOn(StoryChain c, Map map) => true;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            fresh.Clear();
            making = null;
            if (IsStart)
            {
                making = new StoryChain { kind = Story.chain };
                return Setup(making, ctx, ext);
            }
            var c = StoryChains.current;
            if (c == null) return false;
            StoryChains.Fill(ctx, c, ctx.map);
            return ctx.a != null && Prepare(c, ctx, ext);
        }

        /// <summary>A first stage: who and what the story is about (A, X, the neighbors, the sums), and its first flags.</summary>
        protected abstract bool Setup(StoryChain c, HappeningContext ctx, HappeningExtension ext);

        /// <summary>A later stage: anything it needs beyond what the chain remembers.</summary>
        protected virtual bool Prepare(StoryChain c, HappeningContext ctx, HappeningExtension ext) => true;

        /// <summary>People walking in, things left at the gate. False: it can't happen after all.</summary>
        protected virtual bool Arrive(StoryChain c, HappeningContext ctx, HappeningExtension ext) => true;

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            var c = IsStart ? making : StoryChains.current;
            if (c == null || !Arrive(c, ctx, ext)) return false;
            if (IsStart)
            {
                c.x = ctx.stranger;
                c.y = ctx.b;
                StoryChains.Register(c);
            }
            StoryChains.Keep(c, ctx);
            ctx.note = StoryChains.Note(c);
            c.stage++;
            c.next = null;
            c.due = -1;
            c.fallback = null;
            c.tries = 0;
            c.waiting = !ext.options.NullOrEmpty();
            StoryChains.Pin(c.x);
            StoryChains.Pin(c.y);
            making = null;
            fresh.Clear();
            return true;
        }

        protected override void Abort(HappeningContext ctx)
        {
            foreach (var p in fresh)
                if (p != null && !p.Spawned) Arrivals.Discard(p);
            fresh.Clear();
            making = null;
            ctx.stranger = null;
            ctx.b = null;
            ctx.group.Clear();
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            var c = making ?? StoryChains.Of(ctx) ?? StoryChains.current;
            if (c != null)
                foreach (var f in c.flags)
                    tags.Add(f);
            tags.Add(Key);
        }

        /// <summary>Someone new for this stage, kept in the world until they arrive (and thrown away if the stage can't happen).</summary>
        protected Pawn Make(Faction f, FloatRange ages, string lastName = null, Gender? gender = null)
        {
            var kind = f != null ? Arrivals.PersonKind(f) : Arrivals.JoinerKind();
            if (kind == null) return null;
            var p = Arrivals.Make(kind, f, ages, gender, lastName);
            if (p == null) return null;
            fresh.Add(p);
            Arrivals.Hold(p);
            return p;
        }

        // ---------------- The answer ----------------

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            var c = StoryChains.Of(ctx);
            if (c != null && c.Running)
            {
                c.waiting = false;
                if (ctx.map == null || !Find.Maps.Contains(ctx.map)) ctx.map = HappeningTracker.HomeMap();
                try
                {
                    // No home to come back to: the story stops here.
                    if (ctx.map == null) StoryChains.End(c, null);
                    else outcome = Answer(c, ctx, option, risk, byDefault, outcome);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("story answer " + def.defName, e);
                }
                if (c.Running && c.next == null) StoryChains.End(c, null);
                StoryChains.Pin(c.x);
                StoryChains.Pin(c.y);
            }
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }

        /// <summary>
        /// What the answer sets in motion: the next stage (<see cref="StoryChains.Next"/>) or an ending
        /// (<see cref="StoryChains.End"/>). Returns the outcome to show ("" when the ending's own letter tells it).
        /// </summary>
        protected abstract string Answer(StoryChain c, HappeningContext ctx, int option, bool risk, bool byDefault, string outcome);

        protected override void Happened(HappeningContext ctx, HappeningExtension ext, string label)
        {
            base.Happened(ctx, ext, label);
            var c = StoryChains.Of(ctx);
            if (c == null || !c.Running) return;
            c.waiting = false;
            try
            {
                Told(c, ctx);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("story " + def.defName, e);
            }
            if (c.Running && c.next == null) StoryChains.End(c, null);
        }

        /// <summary>A stage without a question was told: what follows.</summary>
        protected virtual void Told(StoryChain c, HappeningContext ctx)
        {
        }

        /// <summary>Someone the story still needs isn't let go when the letter is done with.</summary>
        public override void Cleanup(HappeningContext ctx)
        {
            var x = ctx.stranger;
            if (x != null && StoryChains.Holds(x))
            {
                StoryChains.Pin(x);
                return;
            }
            base.Cleanup(ctx);
        }

        /// <summary>A story of this kind reaches an ending at a set time, with no question asked: its effects, then its letter.</summary>
        public virtual void Ending(StoryChain c, string key, Map map) => StoryChains.End(c, key);

        // ---------------- Helpers ----------------

        protected string Outcome(StoryChain c, HappeningContext ctx, string tag, string fallback, string item = null) =>
            StoryChains.Line(Story?.outcomes, ctx, tag, c.flags, item) ?? fallback;

        /// <summary>A fight the story brings, through Rimfolk's promised raids (they check the battle switch again when they come).</summary>
        protected void Raid(StoryChain c, HappeningContext ctx, Faction f, Pawn leader)
        {
            var t = CouncilGameComponent.Instance?.happenings;
            var map = ctx.map ?? HappeningTracker.HomeMap();
            if (t == null || map == null || !Raids.CanRaid(f)) return;
            var home = StoryChains.HomeText(c.kind);
            ctx.enemy = f;
            string label = StoryChains.Line(home?.raidTitles, ctx, Key, c.flags) ?? def.LabelCap.ToString();
            string text = StoryChains.Line(home?.raid, ctx, Key, c.flags) ?? "";
            t.AddRaid(map, f, Rand.Range(0.5f, 0.7f), Rand.Range(4f, 10f), label, text, leader);
        }

        protected static IntVec3 Near(Pawn p, Map map) => VarietyUtil.Near(p, map);

        protected static string NotEnoughSilver(int n) => "CC_Dilemma_NotEnough".Translate(n, ThingDefOf.Silver.label).ToString();
    }

    // ===================== The door in the hill =====================

    /// <summary>
    /// Out on the land, a colonist finds a sealed door in a hillside. Open it now, study it first (slower, and safer), or
    /// sell the way to it to a neighbor. Behind it, settled when it's found: nothing but writing on the walls (a lesson), a
    /// sleeper in an old casket (who may stay, or may wake wild and run), or a hoard marked with a people's sign (who will
    /// come for it). A neighbor who bought the way comes back cheated, grateful, or with a sleeper they don't want.
    /// </summary>
    public class HappeningWorker_StoryDoor : HappeningWorker_Story
    {
        private const string Empty = "empty", Casket = "casket", Hoard = "hoard";

        private static string Inside(StoryChain c) =>
            c.Has(Casket) ? "Council_Story_DoorCasket" : c.Has(Hoard) ? "Council_Story_DoorHoard" : "Council_Story_DoorEmpty";

        protected override bool CanBegin(Map map, HappeningExtension ext) => StorytellerEvents.Adults(map).Count() >= 2;

        protected override bool Setup(StoryChain c, HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            ctx.a = (Rand.Chance(0.5f) ? VarietyUtil.Best(map, SkillDefOf.Mining) : null) ?? StorytellerEvents.Adults(map).RandomElementWithFallback();
            if (ctx.a == null) return false;
            ctx.placeName = StoryChains.Direction();
            ctx.faction = Arrivals.Friendly(VarietyUtil.OnGoodTerms) ?? Arrivals.Friendly();
            ctx.count = StoryChains.Silver(map, 120f, 260f);
            // What is behind the door is settled now; studying it only finds out first.
            float roll = Rand.Value;
            string inside = roll < 0.35f ? Empty : roll < 0.65f ? Casket : Hoard;
            if (inside == Hoard)
            {
                // The sign on the crates: now and then a hostile people's (who will come for it), else a neighbor's.
                Faction owner = FightsPossible && Rand.Chance(0.5f) ? Raids.Enemy() : null;
                if (owner != null) c.Set("ownershostile");
                else owner = Arrivals.Friendly(f => f != ctx.faction) ?? Arrivals.Friendly();
                if (owner == null) inside = Empty;
                ctx.enemy = owner;
                c.value = Mathf.RoundToInt(Rand.Range(700f, 1200f) * LivingEvents.WealthFactor(map));
            }
            c.Set(inside);
            return true;
        }

        protected override bool CanGoOn(StoryChain c, Map map)
        {
            switch (Key)
            {
                case "doorsleeper": return StoryChains.Alive(c.x) && !c.x.Spawned;
                case "doorowners": return c.enemy != null && !c.enemy.defeated;
                case "doorbuyer": return LivingEvents.Friendly(c.faction);
            }
            return true;
        }

        protected override bool Prepare(StoryChain c, HappeningContext ctx, HappeningExtension ext)
        {
            switch (Key)
            {
                case "doorsleeper":
                    ctx.xWaiting = true;
                    break;
                case "doorbuyer":
                    // {COUNT}: the price they paid; {TALE}: half of it.
                    ctx.tale = (c.amount / 2).ToString();
                    break;
            }
            return true;
        }

        public override string Unavailable(HappeningContext ctx, int option)
        {
            var c = StoryChains.Of(ctx);
            switch (Key)
            {
                case "door":
                    if (option == 2 && !LivingEvents.Friendly(ctx.faction)) return "CC_Story_NoBuyer".Translate().ToString();
                    break;
                case "doorhoard":
                    if (option == 2 && (c == null || c.Has("ownershostile") || !LivingEvents.Friendly(ctx.enemy)))
                        return "CC_Story_OwnersHostile".Translate(ctx.enemy?.Name ?? "?").ToString();
                    break;
                case "doorbuyer":
                    if (option == 1 && VarietyUtil.InStores(ctx.map, ThingDefOf.Silver) < ctx.count / 2) return NotEnoughSilver(ctx.count / 2);
                    break;
            }
            return null;
        }

        /// <summary>Someone asleep in the casket since long before anyone here was born.</summary>
        private bool MakeSleeper(StoryChain c)
        {
            var x = Make(null, new FloatRange(20f, 45f));
            if (x == null) return false;
            c.x = x;
            return true;
        }

        /// <summary>The writing copied out: a share of the research in hand, or a great deal of reading for A.</summary>
        private static bool Lesson(HappeningContext ctx, float share)
        {
            var rm = Find.ResearchManager;
            var proj = rm?.GetProject();
            if (proj != null && !proj.IsFinished)
            {
                float left = proj.Cost - rm.GetProgress(proj);
                float amount = Mathf.Min(left * share, 1800f, left - 1f);
                if (amount > 1f)
                {
                    rm.AddProgress(proj, amount, null);
                    return true;
                }
            }
            var r = ctx.a?.skills?.GetSkill(SkillDefOf.Intellectual);
            if (r != null && !r.TotallyDisabled) r.Learn(2500f, true);
            return false;
        }

        protected override string Answer(StoryChain c, HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            var map = ctx.map;
            switch (Key)
            {
                case "door":
                    if (option == 0)
                    {
                        c.Set("opened");
                        StoryChains.Next(c, Inside(c), 0.8f, 1.4f);
                    }
                    else if (option == 2 && LivingEvents.Friendly(c.faction) && map != null)
                    {
                        c.Set("sold");
                        VarietyUtil.PaySilver(map, Near(ctx.a, map), c.amount);
                        StoryChains.Goodwill(c.faction, 2);
                        if (c.Has(Hoard)) StoryChains.Next(c, "end:grateful", 9f, 14f);
                        else if (c.Has(Casket) && MakeSleeper(c))
                        {
                            c.Set("buyer", "friend");
                            StoryChains.Next(c, "Council_Story_DoorSleeper", 9f, 14f);
                        }
                        else StoryChains.Next(c, "Council_Story_DoorBuyer", 9f, 14f);
                    }
                    else
                    {
                        c.Set("studied");
                        ctx.a?.skills?.GetSkill(SkillDefOf.Intellectual)?.Learn(400f, true);
                        StoryChains.Next(c, Inside(c), 3f, 5f);
                    }
                    return outcome;

                case "doorempty":
                    if (option == 0)
                    {
                        bool research = Lesson(ctx, c.Has("studied") ? 0.35f : 0.25f);
                        StoryChains.Give(ctx.a, "Council_Story_VaultLesson", def);
                        StoryChains.End(c, "lesson", ctx, null, null, research ? "research" : "noresearch");
                    }
                    else StoryChains.End(c, "resealed", ctx);
                    return "";

                case "doorcasket":
                    if (option == 0 && MakeSleeper(c))
                    {
                        // Woken the careful way the carvings told of, a sleeper is likelier to wake calm.
                        bool calm = Rand.Chance(c.Has("studied") ? 0.75f : 0.5f);
                        c.Set(calm ? "friend" : "wary");
                        if (calm || c.Has("studied")) StoryChains.Next(c, "Council_Story_DoorSleeper", 1f, 2f);
                        else StoryChains.Next(c, "end:fled", 1f, 2f);
                        return outcome;
                    }
                    StoryChains.End(c, "sleeping", ctx);
                    return "";

                case "doorhoard":
                    if (option == 0 && map != null)
                    {
                        string item = StoryChains.Gift(map, Near(ctx.a, map), c.value, false);
                        c.Set("tookall");
                        // What the owners will ask for: about half of what it was worth.
                        c.amount = VarietyUtil.Round(Mathf.RoundToInt(c.value * 0.5f));
                        StoryChains.Next(c, "Council_Story_DoorOwners", 10f, 18f);
                        return (outcome ?? "").Replace("{ITEM}", item ?? "");
                    }
                    if (option == 2 && !c.Has("ownershostile") && LivingEvents.Friendly(c.enemy))
                    {
                        StoryChains.Next(c, "end:returned", 4f, 7f);
                        return outcome;
                    }
                    StoryChains.End(c, "share", ctx, map == null ? null : StoryChains.Gift(map, Near(ctx.a, map), c.value * 0.35f, false));
                    return "";

                case "doorsleeper":
                {
                    var x = c.x;
                    if (option == 0 && StoryChains.Alive(x) && map != null && Arrivals.Join(x, map, Near(ctx.a, map)))
                    {
                        ctx.xWaiting = false;
                        StoryChains.Give(ctx.a, "Council_Story_SleeperStayed", def);
                        StoryChains.End(c, "joined", ctx, null, new LookTargets(x));
                    }
                    else
                    {
                        string item = map == null ? null : StoryChains.Gift(map, Near(ctx.a, map), Rand.Range(150f, 300f) * LivingEvents.WealthFactor(map), true);
                        StoryChains.End(c, "parted", ctx, item);
                    }
                    return "";
                }

                case "doorowners":
                {
                    bool hostile = c.Has("ownershostile");
                    // Paid: the silver was taken by the option itself.
                    if (option == 0)
                    {
                        if (!hostile) StoryChains.Goodwill(c.enemy, 10);
                        StoryChains.End(c, "paid", ctx);
                    }
                    else if (!hostile)
                    {
                        StoryChains.Goodwill(c.enemy, -15);
                        StoryChains.End(c, "refused", ctx);
                    }
                    else if (FightsPossible && Raids.CanRaid(c.enemy))
                    {
                        Raid(c, ctx, c.enemy, null);
                        StoryChains.End(c, "fought", ctx);
                    }
                    else StoryChains.End(c, "cursed", ctx);
                    return "";
                }

                case "doorbuyer":
                    if (option == 0)
                    {
                        StoryChains.Goodwill(c.faction, 4);
                        StoryChains.End(c, "refunded", ctx);
                    }
                    else if (option == 1 && VarietyUtil.TakeFromStores(map, ThingDefOf.Silver, c.amount / 2))
                    {
                        StoryChains.Goodwill(c.faction, -3);
                        StoryChains.End(c, "halved", ctx);
                    }
                    else
                    {
                        StoryChains.Goodwill(c.faction, -10);
                        StoryChains.End(c, "kept", ctx);
                    }
                    return "";
            }
            return outcome;
        }

        public override void Ending(StoryChain c, string key, Map map)
        {
            switch (key)
            {
                case "grateful":
                    if (!LivingEvents.Friendly(c.faction)) break;
                    string item = StoryChains.Gift(map, IntVec3.Invalid, c.value * 0.3f, true, true);
                    StoryChains.Goodwill(c.faction, 8);
                    StoryChains.End(c, key, null, item);
                    return;
                case "returned":
                    if (!LivingEvents.Friendly(c.enemy)) break;
                    int fee = VarietyUtil.Round(Mathf.Max(20, Mathf.RoundToInt(c.value * 0.2f)));
                    VarietyUtil.PaySilver(map, DropCellFinder.TradeDropSpot(map), fee);
                    StoryChains.Goodwill(c.enemy, 15);
                    StoryChains.End(c, key, null, fee.ToString());
                    return;
                default:
                    StoryChains.End(c, key);
                    return;
            }
            StoryChains.End(c, null);
        }
    }

    // ===================== A fugitive's debt =====================

    /// <summary>
    /// After dark, a stranger on the run asks for a night's shelter and silence. Hide them (and maybe ask why they run),
    /// or send them on. Riders of a neighbor come asking the next day: lie for the stranger, tell, or sell them out. Weeks
    /// later it comes back: an innocent the colony protected repays the debt (and may stay); a guilty one brings a gift
    /// with a past; an innocent the colony gave up may come back for revenge.
    /// </summary>
    public class HappeningWorker_StoryDebt : HappeningWorker_Story
    {
        protected override bool CanBegin(Map map, HappeningExtension ext) => Arrivals.Friendly(VarietyUtil.OnGoodTerms) != null;

        /// <summary>
        /// The stranger can come back at the head of a war band: out in the world and their own person (never one the colony
        /// took in or holds since), not their own people's leader, not wanted by one of the game's quests.
        /// </summary>
        private static bool Avenger(Pawn x) =>
            StoryChains.Free(x) && !x.Spawned && (x.Faction == null || x.Faction.leader != x) && !QuestUtility.IsReservedByQuestOrQuestBeingGenerated(x);

        protected override bool Setup(StoryChain c, HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            ctx.faction = Arrivals.Friendly(VarietyUtil.OnGoodTerms);
            if (ctx.faction == null) return false;
            // The stranger's own people: anyone not at war with the colony.
            ctx.stranger = Make(Arrivals.Friendly() ?? ctx.faction, new FloatRange(19f, 40f));
            if (ctx.stranger == null) return false;
            ctx.xWaiting = true;
            ctx.a = StorytellerEvents.Adults(map).RandomElementWithFallback();
            if (ctx.a == null) return false;
            ctx.placeName = LivingEvents.TownOf(ctx.faction, map) ?? ctx.faction.Name;
            // The reward the riders will offer.
            ctx.count = StoryChains.Silver(map, 100f, 220f);
            c.Set(Rand.Chance(0.6f) ? "innocent" : "guilty");
            return true;
        }

        protected override bool CanGoOn(StoryChain c, Map map)
        {
            switch (Key)
            {
                case "pursuers": return LivingEvents.Friendly(c.faction) && StoryChains.Alive(c.x);
                case "debtrepaid":
                case "debtgift": return StoryChains.CanVisit(c.x);
                case "debtrevenge": return Avenger(c.x) && FightsPossible && Raids.Enemy() != null;
            }
            return true;
        }

        protected override bool Prepare(StoryChain c, HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            switch (Key)
            {
                case "pursuers":
                    ctx.xWaiting = !c.x.Spawned;
                    break;
                case "debtrepaid":
                    c.value = StoryChains.Silver(map, 350f, 700f);
                    break;
                case "debtgift":
                    c.value = StoryChains.Silver(map, 400f, 800f);
                    break;
                case "debtrevenge":
                    ctx.enemy = Raids.Enemy();
                    ctx.count = StoryChains.Silver(map, 200f, 400f);
                    ctx.xWaiting = true;
                    return ctx.enemy != null;
            }
            return true;
        }

        protected override bool Arrive(StoryChain c, HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            switch (Key)
            {
                case "pursuers":
                {
                    // Two or three riders: they ask their questions and ride on.
                    int n = Rand.RangeInclusive(2, 3);
                    for (int i = 0; i < n; i++)
                    {
                        var p = Make(c.faction, new FloatRange(22f, 50f));
                        if (p != null) ctx.group.Add(p);
                    }
                    return StoryChains.Visit(ctx.group, map, c.faction, null, Rand.Range(6f, 9f));
                }
                case "debtrepaid":
                    if (!StoryChains.Visit(new[] { c.x }, map, c.x.Faction, GatheringSpot(map), Rand.Range(14f, 20f))) return false;
                    ctx.tale = StoryChains.Gift(map, IntVec3.Invalid, c.value, false) ?? "";
                    return true;
                case "debtgift":
                    return StoryChains.Visit(new[] { c.x }, map, c.x.Faction, GatheringSpot(map), Rand.Range(12f, 18f));
            }
            return true;
        }

        public override float RiskChance(HappeningContext ctx, int option, float chance)
        {
            var c = StoryChains.Of(ctx);
            // Nobody can find out about a stranger who was never let in.
            if (Key == "pursuers" && option == 0 && c != null && !c.Has("hid")) return 0f;
            return base.RiskChance(ctx, option, chance);
        }

        protected override string Answer(StoryChain c, HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            var map = ctx.map;
            bool innocent = c.Has("innocent");
            switch (Key)
            {
                case "fugitive":
                    if (option <= 1)
                    {
                        c.Set("hid");
                        if (option == 1) c.Set("asked");
                        // In for the night; gone before dawn.
                        var x = c.x;
                        if (StoryChains.Alive(x) && !x.Spawned && map != null && x.Faction != null && !x.Faction.HostileTo(Faction.OfPlayer)
                            && StoryChains.Visit(new[] { x }, map, x.Faction, GatheringSpot(map), Rand.Range(9f, 13f)))
                            ctx.xWaiting = false;
                        StoryChains.Next(c, "Council_Story_Pursuers", 1f, 1.6f);
                        return option == 1 ? Outcome(c, ctx, "asked", outcome) : outcome;
                    }
                    c.Set("turned");
                    StoryChains.Next(c, "Council_Story_Pursuers", 0.8f, 1.2f);
                    return outcome;

                case "pursuers":
                    if (option == 0)
                    {
                        c.Set("protected");
                        if (risk)
                        {
                            // Someone saw: the riders follow the tracks out of the colony.
                            c.Set("foundout");
                            StoryChains.Goodwill(c.faction, -8);
                            StoryChains.Next(c, "end:caught", 4f, 7f);
                        }
                        else if (c.Has("hid")) StoryChains.Next(c, innocent ? "Council_Story_DebtRepaid" : "Council_Story_DebtGift", 18f, 30f);
                        else StoryChains.Next(c, innocent ? "end:thanks" : "end:gone", 15f, 25f);
                        return outcome;
                    }
                    c.Set("told");
                    if (option == 2)
                    {
                        c.Set("sold");
                        VarietyUtil.PaySilver(map, Near(ctx.a, map), c.amount);
                    }
                    StoryChains.Goodwill(c.faction, option == 2 ? 6 : 4);
                    if (!innocent) StoryChains.Next(c, "end:justice", 6f, 10f);
                    else if (FightsPossible && Raids.Enemy() != null) StoryChains.Next(c, "Council_Story_DebtRevenge", 15f, 25f, "cursed");
                    else StoryChains.Next(c, "end:cursed", 15f, 25f);
                    return outcome;

                case "debtrepaid":
                    if (option == 0 && StoryChains.Alive(c.x) && map != null && Arrivals.Join(c.x, map))
                    {
                        StoryChains.Give(ctx.a, "Council_Story_DebtRepaid", def);
                        StoryChains.End(c, "repaidjoined", ctx, null, new LookTargets(c.x));
                        return "";
                    }
                    StoryChains.Give(ctx.a, "Council_Story_DebtRepaid", def);
                    StoryChains.End(c, "repaid", ctx);
                    return "";

                case "debtgift":
                    if (option == 0 && map != null)
                    {
                        string item = StoryChains.Gift(map, Near(c.x, map), c.value, true);
                        if (risk) StoryChains.Goodwill(c.faction, -10);
                        StoryChains.End(c, risk ? "exposed" : "accepted", ctx, item);
                    }
                    else if (option == 2)
                    {
                        StoryChains.Goodwill(c.faction, 10);
                        StoryChains.End(c, "sentback", ctx);
                    }
                    else StoryChains.End(c, "refusedgift", ctx);
                    return "";

                case "debtrevenge":
                {
                    var x = c.x;
                    // Paid: the silver was taken by the option itself.
                    if (option == 0) StoryChains.End(c, "paidoff", ctx);
                    else if (FightsPossible && Raids.CanRaid(ctx.enemy) && Avenger(x))
                    {
                        x.SetFaction(ctx.enemy);
                        Raid(c, ctx, ctx.enemy, x);
                        StoryChains.Give(ctx.a, "Council_Story_Betrayed", def);
                        StoryChains.End(c, "revenge", ctx);
                    }
                    else
                    {
                        StoryChains.Give(ctx.a, "Council_Story_Betrayed", def);
                        StoryChains.End(c, "cursed", ctx);
                    }
                    return "";
                }
            }
            return outcome;
        }

        public override void Ending(StoryChain c, string key, Map map)
        {
            var ctx = StoryChains.Context(c, StoryChains.HomeDef(c.kind), map);
            switch (key)
            {
                case "thanks":
                {
                    int n = StoryChains.Silver(map, 60f, 140f);
                    VarietyUtil.PaySilver(map, DropCellFinder.TradeDropSpot(map), n);
                    StoryChains.End(c, key, ctx, n.ToString());
                    return;
                }
                case "justice":
                {
                    if (!LivingEvents.Friendly(c.faction))
                    {
                        StoryChains.End(c, null);
                        return;
                    }
                    StoryChains.Goodwill(c.faction, 6);
                    int n = StoryChains.Silver(map, 60f, 120f);
                    VarietyUtil.PaySilver(map, DropCellFinder.TradeDropSpot(map), n);
                    StoryChains.End(c, key, ctx, n.ToString());
                    return;
                }
                case "cursed":
                    StoryChains.Give(ctx.a, "Council_Story_Betrayed", StoryChains.HomeDef(c.kind));
                    break;
            }
            StoryChains.End(c, key, ctx);
        }
    }

    // ===================== Bad blood =====================

    /// <summary>
    /// The heads of two old families of a neighbor town ride in together: one's son and the other's daughter want to marry,
    /// and the fathers haven't spoken in years. Mediate, back one family, sell to both, or stay out of it. Over the weeks it
    /// heals into a wedding at the colony, sours into a blood feud, or leaves the backed family running the town, with the
    /// other one's hired blades on the road.
    /// </summary>
    public class HappeningWorker_StoryFeud : HappeningWorker_Story
    {
        private static readonly string[] Grievances = { "mill", "orchard", "ford" };

        private static Faction Town(Map map) =>
            Arrivals.Friendly(f => VarietyUtil.OnGoodTerms(f) && LivingEvents.TownOf(f, map) != null) ?? Arrivals.Friendly(VarietyUtil.OnGoodTerms);

        protected override bool CanBegin(Map map, HappeningExtension ext) => Town(map) != null && VarietyUtil.Best(map, SkillDefOf.Social) != null;

        protected override bool Setup(StoryChain c, HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            ctx.faction = Town(map);
            if (ctx.faction == null) return false;
            ctx.placeName = LivingEvents.TownOf(ctx.faction, map) ?? ctx.faction.Name;
            ctx.a = VarietyUtil.Best(map, SkillDefOf.Social);
            ctx.stranger = Make(ctx.faction, new FloatRange(45f, 68f));
            ctx.b = Make(ctx.faction, new FloatRange(45f, 68f));
            if (ctx.a == null || ctx.stranger == null || ctx.b == null) return false;
            ctx.dead = StoryChains.Family(ctx.stranger);
            ctx.beast = StoryChains.Family(ctx.b);
            // Two families of the same name would read oddly: the second goes by its head's own name.
            if (ctx.beast == ctx.dead) ctx.beast = ctx.b.LabelShort;
            // What selling supplies to both sides would bring in.
            ctx.count = StoryChains.Silver(map, 150f, 300f);
            c.Set(Grievances.RandomElement());
            return true;
        }

        protected override bool CanGoOn(StoryChain c, Map map)
        {
            if (!LivingEvents.Friendly(c.faction)) return false;
            switch (Key)
            {
                case "feudtalks":
                case "feudsided": return StoryChains.Alive(c.x) && StoryChains.Alive(c.y);
                case "feudvendetta": return FightsPossible && Raids.Enemy() != null;
                case "feudwedding": return StoryChains.CanVisit(c.x) && StoryChains.CanVisit(c.y);
            }
            return true;
        }

        protected override bool Prepare(StoryChain c, HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            switch (Key)
            {
                case "feudsided":
                    // A peace gift to the family that lost out.
                    ctx.count = StoryChains.Silver(map, 120f, 250f);
                    break;
                case "feudvendetta":
                    ctx.enemy = Raids.Enemy();
                    ctx.count = StoryChains.Silver(map, 150f, 300f);
                    return ctx.enemy != null;
                case "feudwedding":
                    ctx.count = StoryChains.Silver(map, 100f, 200f);
                    break;
            }
            return true;
        }

        protected override bool Arrive(StoryChain c, HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            switch (Key)
            {
                case "feud":
                    return StoryChains.Visit(new[] { ctx.stranger, ctx.b }, map, ctx.faction, null, Rand.Range(10f, 16f));
                case "feudwedding":
                {
                    // The fathers, and the young couple: the son of one house and the daughter of the other.
                    string groomName = c.Has("backed") ? c.tale : c.dead;
                    string brideName = c.Has("backed") ? c.relation : c.beast;
                    var groom = Make(c.faction, new FloatRange(18f, 26f), groomName, Gender.Male);
                    var bride = Make(c.faction, new FloatRange(18f, 26f), brideName, Gender.Female);
                    if (groom != null) ctx.group.Add(groom);
                    if (bride != null) ctx.group.Add(bride);
                    return StoryChains.Visit(new[] { c.x, c.y }.Concat(ctx.group), map, c.faction, GatheringSpot(map), Rand.Range(14f, 20f));
                }
            }
            return true;
        }

        /// <summary>The colony backs one family: from here on X and {TALE} are the backed house, B and {RELATION} the other.</summary>
        private static void Back(StoryChain c, bool first)
        {
            if (!first)
            {
                var p = c.x;
                c.x = c.y;
                c.y = p;
            }
            c.tale = first ? c.dead : c.beast;
            c.relation = first ? c.beast : c.dead;
            c.Set("backed");
            StoryChains.Goodwill(c.faction, 3);
            StoryChains.Next(c, "Council_Story_FeudSided", 6f, 9f, "grudge");
        }

        private static void Talks(StoryChain c, HappeningContext ctx, float chance)
        {
            int social = ctx.a?.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0;
            if (Rand.Chance(Mathf.Clamp01(chance + social * 0.01f))) StoryChains.Next(c, "Council_Story_FeudWedding", 6f, 10f, "ownpeace");
            else StoryChains.Next(c, "end:blood", 6f, 10f);
        }

        private static void Vendetta(StoryChain c, float minDays, float maxDays)
        {
            if (FightsPossible && Raids.Enemy() != null) StoryChains.Next(c, "Council_Story_FeudVendetta", minDays, maxDays, "grudge");
            else StoryChains.Next(c, "end:grudge", minDays, maxDays);
        }

        protected override string Answer(StoryChain c, HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            var map = ctx.map;
            switch (Key)
            {
                case "feud":
                    switch (option)
                    {
                        case 0:
                        {
                            c.Set("mediate");
                            int social = ctx.a?.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0;
                            c.Set(Rand.Chance(Mathf.Clamp(0.35f + social * 0.03f, 0.2f, 0.9f)) ? "good" : "bad");
                            StoryChains.Next(c, "Council_Story_FeudTalks", 5f, 8f, "goeson");
                            break;
                        }
                        case 1:
                        case 2:
                            Back(c, option == 1);
                            break;
                        case 3:
                            c.Set("profit");
                            VarietyUtil.PaySilver(map, Near(ctx.a, map), c.amount);
                            StoryChains.Next(c, "end:bloodsilver", 10f, 16f);
                            break;
                        default:
                            c.Set("out");
                            StoryChains.Next(c, Rand.Chance(0.5f) ? "end:ownpeace" : "end:goeson", 12f, 20f);
                            break;
                    }
                    return outcome;

                case "feudtalks":
                    if (option == 0)
                    {
                        // The two fathers come to the colony to talk (the food was set aside by the option itself).
                        if (StoryChains.CanVisit(c.x) && StoryChains.CanVisit(c.y))
                            StoryChains.Visit(new[] { c.x, c.y }, map, c.faction, GatheringSpot(map), Rand.Range(10f, 14f));
                        Talks(c, ctx, c.Has("good") ? 0.8f : 0.5f);
                    }
                    else Talks(c, ctx, c.Has("good") ? 0.5f : 0.2f);
                    return outcome;

                case "feudsided":
                    if (option == 0)
                    {
                        // The gift went with the option; it may or may not be enough.
                        if (Rand.Chance(0.7f)) StoryChains.Next(c, "end:alliance", 4f, 8f);
                        else Vendetta(c, 4f, 8f);
                    }
                    else
                    {
                        StoryChains.Goodwill(c.faction, 4);
                        Vendetta(c, 5f, 9f);
                    }
                    return outcome;

                case "feudvendetta":
                    if (option == 0) StoryChains.End(c, "boughtoff", ctx);
                    else if (FightsPossible && Raids.CanRaid(ctx.enemy))
                    {
                        Raid(c, ctx, ctx.enemy, null);
                        StoryChains.Goodwill(c.faction, 8);
                        StoryChains.End(c, "vendetta", ctx);
                    }
                    else StoryChains.End(c, "grudge", ctx);
                    return "";

                case "feudwedding":
                    if (option == 0)
                    {
                        // The feast's food went with the option.
                        foreach (var p in StorytellerEvents.Adults(map).ToList()) StoryChains.Give(p, "Council_Story_Wedding", def);
                        foreach (var p in IncidentWorker_Happening.Colonists(map)) p.needs?.joy?.GainJoy(0.2f, JoyKindDefOf.Social);
                        StoryChains.Goodwill(c.faction, 15);
                        StoryChains.End(c, "wedding", ctx);
                        return "";
                    }
                    Arrivals.SendAway(new[] { c.x, c.y }.Concat(ctx.group));
                    StoryChains.Goodwill(c.faction, option == 1 ? 8 : 3);
                    StoryChains.End(c, option == 1 ? "weddinggift" : "weddingwished", ctx);
                    return "";
            }
            return outcome;
        }

        public override void Ending(StoryChain c, string key, Map map)
        {
            if (c.faction == null || c.faction.defeated)
            {
                StoryChains.End(c, null);
                return;
            }
            var ctx = StoryChains.Context(c, StoryChains.HomeDef(c.kind), map);
            switch (key)
            {
                case "alliance":
                {
                    StoryChains.Goodwill(c.faction, 10);
                    string item = StoryChains.Gift(map, IntVec3.Invalid, Rand.Range(200f, 400f) * LivingEvents.WealthFactor(map), false);
                    StoryChains.End(c, key, ctx, item);
                    return;
                }
                case "blood":
                    StoryChains.Goodwill(c.faction, -5);
                    StoryChains.Give(ctx.a, "Council_Story_FailedPeace", StoryChains.HomeDef(c.kind));
                    break;
                case "bloodsilver":
                    StoryChains.Goodwill(c.faction, -12);
                    break;
                case "grudge":
                    StoryChains.Goodwill(c.faction, -4);
                    break;
            }
            StoryChains.End(c, key, ctx);
        }
    }

    // ===================== The preacher =====================

    /// <summary>
    /// A wandering preacher and a few followers stop on the road by the colony and preach to anyone who'll listen.
    /// Let them, argue with them, or send them on. One of the colony may want to follow the preacher; an argument lost
    /// leaves the colony split; a preacher sent away settles on a hill nearby, and the pilgrims start coming.
    /// </summary>
    public class HappeningWorker_StoryProphet : HappeningWorker_Story
    {
        protected override bool CanBegin(Map map, HappeningExtension ext) => Arrivals.Friendly() != null && StorytellerEvents.Adults(map).Count() >= 3;

        protected override bool Setup(StoryChain c, HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            ctx.faction = Arrivals.Friendly();
            if (ctx.faction == null) return false;
            ctx.stranger = Make(ctx.faction, new FloatRange(35f, 70f));
            if (ctx.stranger == null) return false;
            StorytellerEvents.Skilled(ctx.stranger, SkillDefOf.Social, new IntRange(10, 16));
            for (int i = 0; i < 2; i++)
            {
                var p = Make(ctx.faction, new FloatRange(18f, 50f));
                if (p != null) ctx.group.Add(p);
            }
            ctx.a = VarietyUtil.Best(map, SkillDefOf.Social) ?? StorytellerEvents.Adults(map).RandomElementWithFallback();
            if (ctx.a == null) return false;
            // With Ideology, the preacher speaks for a way of their own: {TALE} names it.
            var ideo = ModsConfig.IdeologyActive ? ctx.stranger.Ideo : null;
            if (ideo != null && ideo != Faction.OfPlayer.ideos?.PrimaryIdeo && !ideo.name.NullOrEmpty())
            {
                c.Set("ideo");
                ctx.tale = ideo.name;
            }
            return true;
        }

        /// <summary>Who in the colony might want to walk with the preacher: someone unsettled, or (with Ideology) unsure of their own faith.</summary>
        private static Pawn Listener(Map map, Pawn not)
        {
            return StorytellerEvents.Adults(map).Where(p => p != not && !(p.story?.traits?.HasTrait(TraitDefOf.Psychopath) ?? false) && !p.IsQuestLodger())
                .RandomElementByWeightWithFallback(p =>
                {
                    float w = 1f + (1f - (p.needs?.mood?.CurLevelPercentage ?? 0.5f)) * 2f;
                    if (ModsConfig.IdeologyActive && p.ideo != null) w += (1f - p.ideo.Certainty) * 3f;
                    return w;
                });
        }

        protected override bool CanGoOn(StoryChain c, Map map)
        {
            switch (Key)
            {
                case "prophetfollowers": return StoryChains.Free(c.x) && LivingEvents.Friendly(c.faction) && Listener(map, StoryChains.Hero(c, map)) != null;
                case "prophetschism": return StorytellerEvents.Adults(map).Count() >= 2;
                case "prophethill":
                case "prophetpilgrims": return LivingEvents.Friendly(c.faction);
            }
            return true;
        }

        protected override bool Prepare(StoryChain c, HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            switch (Key)
            {
                case "prophetfollowers":
                    ctx.b = Listener(map, ctx.a);
                    c.y = ctx.b;
                    ctx.xWaiting = true;
                    return ctx.b != null;
                case "prophetschism":
                {
                    // Someone who sides with the preacher's words: the preacher, if they stayed, else whoever was swayed.
                    var x = c.x;
                    ctx.b = x != null && x.IsColonist && x.Spawned && x.Map == map && x != ctx.a ? x
                        : StorytellerEvents.Adults(map).Where(p => p != ctx.a).RandomElementWithFallback();
                    return ctx.b != null;
                }
                case "prophethill":
                case "prophetpilgrims":
                    ctx.count = StoryChains.Silver(map, 60f, 140f);
                    break;
            }
            return true;
        }

        protected override bool Arrive(StoryChain c, HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            switch (Key)
            {
                case "prophet":
                    return StoryChains.Visit(ctx.group.Prepend(ctx.stranger), map, ctx.faction, null, Rand.Range(18f, 26f));
                // apart: a story event splitting the colony, told in its letter.
                case "prophetschism":
                    CouncilGameComponent.Instance?.social.Bump(ctx.a, ctx.b, TieKind.Rivals, 10f, def.label);
                    foreach (var p in StorytellerEvents.Adults(map).Where(p => p != ctx.a && p != ctx.b && Rand.Chance(0.5f)).ToList())
                        StoryChains.Give(p, "Council_Story_Schism", def);
                    return true;
                case "prophethill":
                case "prophetpilgrims":
                {
                    int n = Key == "prophethill" ? 3 : Rand.RangeInclusive(3, 5);
                    for (int i = 0; i < n; i++)
                    {
                        var p = Make(c.faction, new FloatRange(18f, 70f));
                        if (p != null) ctx.group.Add(p);
                    }
                    return StoryChains.Visit(ctx.group, map, c.faction, GatheringSpot(map), Rand.Range(10f, 14f));
                }
            }
            return true;
        }

        protected override string Answer(StoryChain c, HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            var map = ctx.map;
            switch (Key)
            {
                case "prophet":
                    if (option == 1)
                    {
                        var a = ctx.a;
                        int social = a?.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0;
                        int wits = a?.skills?.GetSkill(SkillDefOf.Intellectual)?.Level ?? 0;
                        if (Rand.Chance(Mathf.Clamp(0.3f + social * 0.03f + (wits >= 10 ? 0.1f : 0f), 0.15f, 0.85f)))
                        {
                            c.Set("won");
                            StoryChains.Give(a, "Council_Story_WonDebate", def);
                            // One of the followers has heard enough, and asks to stay.
                            var follower = ctx.group.FirstOrDefault(p => p != null && !p.Dead && p.Spawned && p.Map == map);
                            if (follower != null && Arrivals.Join(follower, map))
                            {
                                ctx.relation = c.relation = follower.LabelShort;
                                StoryChains.End(c, "humbled", ctx, null, new LookTargets(follower), "follower");
                            }
                            else StoryChains.End(c, "humbled", ctx, null, null, "nofollower");
                            return "";
                        }
                        c.Set("lost");
                        StoryChains.Give(a, "Council_Story_LostDebate", def);
                        StoryChains.Next(c, "Council_Story_ProphetSchism", 3f, 5f, "split");
                        return Outcome(c, ctx, "lost", outcome);
                    }
                    if (option == 2)
                    {
                        c.Set("sentaway");
                        Arrivals.SendAway(ctx.group.Prepend(ctx.stranger));
                        StoryChains.Next(c, "Council_Story_ProphetHill", 20f, 35f);
                        return outcome;
                    }
                    c.Set("tolerate");
                    foreach (var p in StorytellerEvents.Adults(map).Where(p => Rand.Chance(0.5f)).ToList()) StoryChains.Give(p, "Council_Story_Sermons", def);
                    StoryChains.Next(c, "Council_Story_ProphetFollowers", 4f, 6f, "movedon");
                    return outcome;

                case "prophetfollowers":
                {
                    var b = c.y;
                    // Only from home: someone sent off from a caravan in the wilds might not live to reach the road. Only on
                    // their own feet and in their right mind, and never the last of the colony at home.
                    if (option == 0 && b != null && b.IsColonist && !b.Dead && b.Spawned && b.Map == map && map.IsPlayerHome && !b.Downed && !b.InMentalState
                        && StorytellerEvents.Adults(map).Any(p => p != b))
                    {
                        // They walk out to join the preacher on the road: a colonist who quit, as the colony's records see it.
                        // As when someone quits: their open wishes and the council's marks on them go first.
                        var comp = CouncilGameComponent.Instance;
                        if (comp != null)
                            foreach (var w in comp.wishes.Where(w => w.state == WishState.Open && w.Involves(b)).ToList())
                                comp.Resolve(w, WishState.Cancelled);
                        TrustEffects.StripAll(b);
                        if (b.Drafted) b.drafter.Drafted = false;
                        var t = comp?.happenings;
                        if (t != null) t.quitting = b;
                        try
                        {
                            PawnBanishUtility.Banish(b, false);
                        }
                        finally
                        {
                            if (t != null) t.quitting = null;
                        }
                        StoryChains.Next(c, "end:convert", 25f, 40f);
                        return outcome;
                    }
                    if (option == 2 && StoryChains.Free(c.x) && map != null && Arrivals.Join(c.x, map))
                    {
                        if (Rand.Chance(0.5f))
                        {
                            StoryChains.Next(c, "Council_Story_ProphetSchism", 4f, 7f, "newvoice");
                            return outcome;
                        }
                        StoryChains.End(c, "newvoice", ctx, null, new LookTargets(c.x));
                        return "";
                    }
                    StoryChains.Give(b, "Council_Story_KeptBack", def);
                    StoryChains.End(c, "stayed", ctx);
                    return "";
                }

                case "prophetschism":
                {
                    // apart: the colony's answer to a story event, told in its letter.
                    int social = ctx.a?.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0;
                    float heal = option == 0 ? Mathf.Clamp(0.4f + social * 0.03f, 0.3f, 0.9f) : 0.35f;
                    if (Rand.Chance(heal))
                    {
                        foreach (var p in StorytellerEvents.Adults(map).ToList()) StoryChains.Give(p, "Council_Story_Healed", def);
                        CouncilGameComponent.Instance?.social.Weaken(ctx.a, ctx.b, TieKind.Rivals, 15f, def.label);
                        StoryChains.End(c, "healed", ctx);
                    }
                    else StoryChains.End(c, "split", ctx);
                    return "";
                }

                case "prophethill":
                    if (option == 0)
                    {
                        VarietyUtil.PaySilver(map, Near(ctx.group.FirstOrDefault(), map), c.amount);
                        c.extra = 1;
                        StoryChains.Next(c, "Council_Story_ProphetPilgrims", 14f, 22f, "shrine");
                        return outcome;
                    }
                    Arrivals.SendAway(ctx.group);
                    StoryChains.End(c, "turnedaway", ctx);
                    return "";
            }
            return outcome;
        }

        protected override void Told(StoryChain c, HappeningContext ctx)
        {
            if (Key != "prophetpilgrims") return;
            VarietyUtil.PaySilver(ctx.map, DropCellFinder.TradeDropSpot(ctx.map), c.amount);
            c.extra++;
            StoryChains.Next(c, "end:shrine", 12f, 20f);
        }

        public override void Ending(StoryChain c, string key, Map map)
        {
            var ctx = StoryChains.Context(c, StoryChains.HomeDef(c.kind), map);
            switch (key)
            {
                case "convert":
                    // Word from the one who left with the preacher (if they're still out there, and haven't come home since).
                    if (!StoryChains.Alive(c.y) || c.y.Faction == Faction.OfPlayer)
                    {
                        StoryChains.End(c, null);
                        return;
                    }
                    break;
                case "shrine":
                {
                    if (!LivingEvents.Friendly(c.faction)) break;
                    int n = StoryChains.Silver(map, 100f, 200f);
                    VarietyUtil.PaySilver(map, DropCellFinder.TradeDropSpot(map), n);
                    StoryChains.End(c, key, ctx, n.ToString());
                    return;
                }
            }
            StoryChains.End(c, key, ctx);
        }
    }

    // ===================== The lost expedition =====================

    /// <summary>
    /// The last of an expedition reaches the colony, worn out, with a map to the ruin where the others were left. Outfit a
    /// rescue, send word to their people, or give them what can be spared and let them go back alone. Settled now: the
    /// others are alive, some are, or none. A rescue comes back with survivors and finds, or with a single survivor and a
    /// warning that something followed them out of the ruin.
    /// </summary>
    public class HappeningWorker_StoryExpedition : HappeningWorker_Story
    {
        protected override bool CanBegin(Map map, HappeningExtension ext) => Arrivals.Friendly() != null;

        protected override bool Setup(StoryChain c, HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            ctx.faction = Arrivals.Friendly();
            if (ctx.faction == null) return false;
            ctx.stranger = Make(ctx.faction, new FloatRange(25f, 55f));
            if (ctx.stranger == null) return false;
            StorytellerEvents.Skilled(ctx.stranger, SkillDefOf.Intellectual, new IntRange(8, 14));
            ctx.relation = ctx.stranger.LabelShort;
            // The old adventurer who settled here (Workers_Storyteller.cs) knows the roads, and draws the rescue a better map.
            var adventurer = CouncilGameComponent.Instance?.happenings.Memos(StorytellerEvents.Adventurer)
                .Select(m => m.pawn).FirstOrDefault(p => StorytellerEvents.Able(p) && p.IsColonist && p.Map == map);
            if (adventurer != null) c.Set("adventurer");
            ctx.a = adventurer ?? VarietyUtil.Best(map, SkillDefOf.Social) ?? StorytellerEvents.Adults(map).RandomElementWithFallback();
            if (ctx.a == null) return false;
            ctx.placeName = StoryChains.Line(Story?.names, ctx, null, null) ?? "";
            // What became of the others is settled now.
            float roll = Rand.Value;
            float alive = adventurer != null ? 0.55f : 0.45f, half = adventurer != null ? 0.3f : 0.35f;
            c.Set(roll < alive ? "alive" : roll < alive + half ? "half" : "lost");
            // The guides' pay, on top of the food.
            ctx.count = StoryChains.Silver(map, 80f, 160f);
            c.value = Mathf.RoundToInt(Rand.Range(500f, 900f) * LivingEvents.WealthFactor(map));
            return true;
        }

        protected override bool CanGoOn(StoryChain c, Map map)
        {
            switch (Key)
            {
                case "expeditionreturn": return StoryChains.CanVisit(c.x);
                case "expeditionwarning": return Arrivals.Friendly() != null;
            }
            return true;
        }

        protected override bool Arrive(StoryChain c, HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            switch (Key)
            {
                case "expedition":
                    return StoryChains.Visit(new[] { ctx.stranger }, map, ctx.faction, GatheringSpot(map), Rand.Range(16f, 24f));
                case "expeditionreturn":
                {
                    int n = c.Has("alive") ? Rand.RangeInclusive(2, 3) : 1;
                    for (int i = 0; i < n; i++)
                    {
                        var p = Make(c.faction, new FloatRange(20f, 50f));
                        if (p != null) ctx.group.Add(p);
                    }
                    ctx.b = ctx.group.FirstOrDefault();
                    if (!StoryChains.Visit(ctx.group.Prepend(c.x), map, c.faction, GatheringSpot(map), Rand.Range(16f, 24f))) return false;
                    bool alive = c.Has("alive");
                    ctx.tale = StoryChains.Gift(map, IntVec3.Invalid, alive ? c.value : c.value * 0.6f, !alive) ?? "";
                    return true;
                }
                case "expeditionwarning":
                {
                    // A survivor alone (the explorer's own people, or anyone not at war with the colony).
                    var f = LivingEvents.Friendly(c.faction) ? c.faction : Arrivals.Friendly();
                    var y = Make(f, new FloatRange(20f, 45f));
                    if (y == null) return false;
                    ctx.b = y;
                    ctx.enemy = Raids.Enemy();
                    if (ctx.enemy == null) c.Set("noenemy");
                    return StoryChains.Visit(new[] { y }, map, f, GatheringSpot(map), Rand.Range(12f, 20f));
                }
            }
            return true;
        }

        protected override string Answer(StoryChain c, HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            var map = ctx.map;
            switch (Key)
            {
                case "expedition":
                    if (option == 0)
                    {
                        // The food and the guides' pay went with the option; the explorer leads the rescue back.
                        c.Set("rescue");
                        Arrivals.SendAway(new[] { c.x });
                        if (c.Has("lost")) StoryChains.Next(c, "Council_Story_ExpeditionWarning", 11f, 16f, "lostall");
                        else StoryChains.Next(c, "Council_Story_ExpeditionReturn", 9f, 14f, "lostall");
                    }
                    else if (option == 1)
                    {
                        c.Set("word");
                        StoryChains.Goodwill(c.faction, 4);
                        StoryChains.Next(c, "end:theirrescue", 10f, 16f);
                    }
                    else
                    {
                        c.Set("alone");
                        StoryChains.Next(c, "end:alone", 15f, 25f);
                    }
                    return outcome;

                case "expeditionreturn":
                {
                    var b = ctx.b;
                    if (option == 0 && b != null && !b.Dead && b.Spawned && map != null && Arrivals.Join(b, map))
                    {
                        StoryChains.Give(ctx.a, "Council_Story_Rescued", def);
                        StoryChains.End(c, "joined", ctx, null, new LookTargets(b));
                        return "";
                    }
                    StoryChains.Goodwill(c.faction, 10);
                    StoryChains.Give(ctx.a, "Council_Story_Rescued", def);
                    StoryChains.End(c, "home", ctx);
                    return "";
                }

                case "expeditionwarning":
                {
                    var y = ctx.b;
                    bool fight = FightsPossible && Raids.CanRaid(ctx.enemy);
                    if (option == 0 && y != null && !y.Dead && map != null && Arrivals.Join(y, map))
                    {
                        if (fight)
                        {
                            Raid(c, ctx, ctx.enemy, null);
                            StoryChains.End(c, "guardians", ctx);
                        }
                        else StoryChains.End(c, "warned", ctx);
                        return "";
                    }
                    if (y != null) Arrivals.SendAway(new[] { y });
                    if (fight && Rand.Chance(0.4f))
                    {
                        Raid(c, ctx, ctx.enemy, null);
                        StoryChains.End(c, "followed", ctx);
                    }
                    else StoryChains.End(c, "passedby", ctx);
                    return "";
                }
            }
            return outcome;
        }

        public override void Ending(StoryChain c, string key, Map map)
        {
            var ctx = StoryChains.Context(c, StoryChains.HomeDef(c.kind), map);
            bool lost = c.Has("lost");
            switch (key)
            {
                case "theirrescue":
                    if (!lost && LivingEvents.Friendly(c.faction))
                    {
                        StoryChains.Goodwill(c.faction, 6);
                        int n = StoryChains.Silver(map, 100f, 220f);
                        VarietyUtil.PaySilver(map, DropCellFinder.TradeDropSpot(map), n);
                        StoryChains.End(c, key, ctx, n.ToString());
                        return;
                    }
                    break;
                case "alone":
                    if (c.Has("alive") && LivingEvents.Friendly(c.faction))
                    {
                        string item = StoryChains.Gift(map, IntVec3.Invalid, Rand.Range(150f, 300f) * LivingEvents.WealthFactor(map), false, true);
                        StoryChains.End(c, key, ctx, item);
                        return;
                    }
                    break;
            }
            StoryChains.End(c, key, ctx);
        }
    }
}
