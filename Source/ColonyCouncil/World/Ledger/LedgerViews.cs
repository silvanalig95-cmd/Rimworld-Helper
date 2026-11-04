using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// How a people (or a town) stands with a colony, as Rimfolk works it out today: the game's goodwill and war, the
    /// tone of what the people remember (<see cref="MemoryMath.Tone"/>, as FactionMemory.Tone), and a regard of -100 to
    /// 100 (<see cref="NotableMath.Baseline"/> from goodwill and memory, as Outsiders.OfColony gives it for someone of
    /// theirs with no nature to speak of; for a town whose head the colony knows, the head's own regard,
    /// NotableBook.Opinion, as the letters to them read it).
    /// </summary>
    public sealed class StanceView
    {
        public int goodwill;
        public bool hostile;
        /// <summary>0 nothing to speak of, 1 well disposed, 2 in the colony's debt, 3 wary, 4 a grudge, 5 grateful and aggrieved, 6 the colony owes them (CC_Memory_Tone_N).</summary>
        public int tone;
        /// <summary>-100 to 100.</summary>
        public int opinion;
        /// <summary>The regard in words, 0 (holds the colony dear) to 6 (hostile): NotableMath.Level, CC_Notable_Regard_N.</summary>
        public int level;
        /// <summary>The head's regard stands for the town's (a town whose head the colony knows).</summary>
        public bool byHead;

        public StanceView ByHead(int headOpinion)
        {
            var s = (StanceView)MemberwiseClone();
            s.opinion = headOpinion;
            s.level = NotableMath.Level(headOpinion);
            s.byHead = true;
            return s;
        }
    }

    /// <summary>A stance toward one colony. With one stance for the whole player faction today, every colony gets the same.</summary>
    public sealed class ColonyStance
    {
        public Ref colony;
        public StanceView stance;
    }

    /// <summary>A notable of a town: who, what they are to it, and how they regard the colony.</summary>
    public sealed class NotableView
    {
        public Ref person;
        /// <summary>Their record in the notables (NotableBook.Get).</summary>
        public int id;
        public Pawn pawn;
        public string name;
        public NotableRole role;
        /// <summary>"mayor", "chief", "heir", "envoy": NotableBook.TitleOf.</summary>
        public string title;
        public bool alive;
        /// <summary>-100 to 100 (NotableBook.Opinion).</summary>
        public int opinion;
    }

    /// <summary>A world situation touching a town or a people: running, or ended lately.</summary>
    public sealed class EventView
    {
        /// <summary>The running situation's id (WorldSituations.Get); -1 for one that has ended.</summary>
        public int id = -1;
        public string defName;
        /// <summary>Its own name, or what it is.</summary>
        public string label;
        public bool running;
        /// <summary>It is about this people (their famine); <see cref="against"/>: they are its enemy.</summary>
        public bool theirs;
        public bool against;
        public int start;
        public int end;
        public bool helped;
    }

    /// <summary>News of a town told at the colony ("Heard lately").</summary>
    public sealed class HeardView
    {
        public string text;
        public string teller;
        public string told;
        public int tick;
    }

    /// <summary>A town: its name and people, who the colony knows there, its mood toward each colony, what touches it, and whether it stands.</summary>
    public sealed class TownView
    {
        public Ref town;
        public int id;
        public string name;
        public Faction people;
        public Ref peopleRef;
        /// <summary>The settlement still stands (then <see cref="settlement"/> is it).</summary>
        public bool stands;
        public Settlement settlement;
        /// <summary>The notables of the town (head, heir, envoy, trader, then faces met who live there).</summary>
        public List<NotableView> notables = new List<NotableView>();
        /// <summary>Its head, alive, if the colony knows them.</summary>
        public NotableView head;
        /// <summary>Its mood toward the player faction: its people's stance, or its head's regard when known.</summary>
        public StanceView overall;
        public List<ColonyStance> mood = new List<ColonyStance>();
        /// <summary>World situations about its people, against them or at the town, running or ended in the last fifteen days.</summary>
        public List<EventView> events = new List<EventView>();
        /// <summary>Heard lately, newest first.</summary>
        public List<HeardView> heard = new List<HeardView>();

        public StanceView MoodToward(Ref colony) => mood.FirstOrDefault(m => m.colony == colony)?.stance ?? overall;
    }

    /// <summary>One thing a people remembers of the colony (a FactionDeed, copied).</summary>
    public sealed class MemoryView
    {
        public int id;
        public string kind;
        public DeedSide side;
        public int weight;
        /// <summary>How strongly it is remembered now.</summary>
        public float strength;
        public int tick;
        public int first;
        public int count;
        public string name;
        public bool settled;
        public Pawn by;
        public string byName;
    }

    /// <summary>A war of a people's with another, as the colony saw it (WarSeen).</summary>
    public sealed class WarView
    {
        public Faction other;
        public Ref otherRef;
        /// <summary>When it was first seen (-1: before the colony watched), when it ended (-1: still on), when their fighters last met on the colony's land.</summary>
        public int start = -1;
        public int end = -1;
        public int fought = -1;

        public bool On => end < 0;
    }

    /// <summary>A people: its stance toward each colony, what it remembers, its wars, its age.</summary>
    public sealed class PeopleView
    {
        public Faction faction;
        public Ref people;
        public string name;
        public StanceView overall;
        public List<ColonyStance> stance = new List<ColonyStance>();
        /// <summary>The strongest first.</summary>
        public List<MemoryView> remembers = new List<MemoryView>();
        /// <summary>The newest first.</summary>
        public List<WarView> wars = new List<WarView>();
        public WorldAge age;
        /// <summary>A people of royal titles (the Empire): its own titles and honour, apart from the ages.</summary>
        public bool empire;

        public StanceView StanceToward(Ref colony) => stance.FirstOrDefault(m => m.colony == colony)?.stance ?? overall;
    }

    /// <summary>Someone of the colony who knows a person out in the world, and how.</summary>
    public sealed class MetView
    {
        public Pawn colonist;
        public string name;
        /// <summary>
        /// How: a notable's way of meeting (freed, tended, camp, guest, trader, visitor, envoy), "letters" (a notable's
        /// regard of a colonist), "friend" or "talked" (an outsider pair), "regular" (the colonist a regular asks after),
        /// "colonist" (a departed ex-colonist: they were one of the colony).
        /// </summary>
        public string how;
        public int tick = -1;
    }

    /// <summary>A person out in the world: name, people, town, role, and who of the colony met them.</summary>
    public sealed class PersonView
    {
        public Ref person;
        /// <summary>Their pawn while the game keeps one.</summary>
        public Pawn pawn;
        public string name;
        public Faction people;
        public int town = -1;
        public string townName;
        /// <summary>head, heir, envoy, trader, acquaintance (a notable); regular; departed; visitor (someone the colony talked with); or null.</summary>
        public string role;
        /// <summary>A notable's title ("mayor", "envoy"), or null.</summary>
        public string title;
        /// <summary>A regular's trade on the road (tinker, scholar, doctor...), or null.</summary>
        public string trade;
        /// <summary>Their record in the notables, or -1.</summary>
        public int notable = -1;
        public bool alive;
        /// <summary>Notables' records first, then outsider pairs, regulars and departed ex-colonists; one line a colonist.</summary>
        public List<MetView> metBy = new List<MetView>();
    }

    /// <summary>
    /// The ledger's views of a town, a people and a person, read from today's stores on demand and kept for one
    /// 250-tick stretch (Lean/Fresh.cs), sooner when the faction memory, the notables or the ledger change.
    /// </summary>
    public partial class WorldLedger
    {
        private const float EndedLatelyDays = 15f;

        private static Game viewsGame;
        private static readonly KeptAnswers<TownView> towns = new KeptAnswers<TownView>(Sched.RareTicks);
        private static readonly KeptAnswers<PeopleView> peoples = new KeptAnswers<PeopleView>(Sched.RareTicks);
        private static readonly KeptAnswers<PersonView> personViews = new KeptAnswers<PersonView>(Sched.RareTicks);

        /// <summary>What the views are read from changed (the ledger, the faction memory, the notables).</summary>
        private static int SourcesVersion => unchecked(version + FactionMemory.Version + NotableBook.Version);

        private static void CheckViews()
        {
            if (viewsGame == Current.Game) return;
            viewsGame = Current.Game;
            towns.Clear();
            peoples.Clear();
            personViews.Clear();
        }

        private static T Kept<T>(KeptAnswers<T> kept, int key, Func<T> build, string what) where T : class
        {
            if (Current.Game == null) return null;
            CheckViews();
            int now = Now, v = SourcesVersion;
            if (kept.TryGet(key, now, v, out var found)) return found;
            T made = null;
            try
            {
                made = build();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("world ledger: " + what, e);
            }
            kept.Set(key, now, v, made);
            return made;
        }

        // ---------------- Stances ----------------

        /// <summary>How a people stands with the player faction (see <see cref="StanceView"/>).</summary>
        private static StanceView StanceOf(Faction f)
        {
            var s = new StanceView();
            if (f == null) return s;
            s.goodwill = f.HasGoodwill ? f.PlayerGoodwill : 0;
            s.hostile = f.HostileTo(Faction.OfPlayer);
            float favor = 0f, wrong = 0f, owed = 0f;
            var mem = FactionMemory.Instance;
            if (mem != null && FactionMemory.On)
            {
                favor = mem.Sum(f, DeedSide.Favor);
                wrong = mem.Sum(f, DeedSide.Wrong);
                owed = mem.Sum(f, DeedSide.TheirFavor, true);
            }
            s.tone = MemoryMath.Tone(favor, wrong, owed);
            s.opinion = NotableMath.Baseline(s.goodwill, favor, wrong, owed, 0, s.hostile);
            s.level = NotableMath.Level(s.opinion);
            return s;
        }

        /// <summary>One stance per colony. Today a people has one stance toward the whole player faction, so each colony gets it.</summary>
        private static List<ColonyStance> PerColony(StanceView s)
        {
            var list = new List<ColonyStance>();
            foreach (var home in Homes.All)
                list.Add(new ColonyStance { colony = Refs.Colony(home), stance = s });
            return list;
        }

        // ---------------- A town ----------------

        /// <summary>
        /// A town by its settlement's id: standing, or gone but still remembered by a store (a notable of it, news heard of
        /// it). Null for a town nobody knows.
        /// </summary>
        public static TownView Town(int settlementId) => settlementId < 0 ? null : Kept(towns, settlementId, () => BuildTown(settlementId), "a town's view");

        private static TownView BuildTown(int id)
        {
            var s = NotableBook.TownOf(id);
            // One of the player's own is a colony (a Ref.Colony), not a town.
            if (s?.Faction != null && s.Faction.IsPlayer) return null;
            var book = NotableBook.Instance;
            var outs = OutsiderBook.Instance;
            var v = new TownView { id = id, town = Ref.Town(id), settlement = s, stands = s != null && !s.Destroyed };
            v.name = s != null && s.HasName ? s.Name : null;
            v.people = s?.Faction;
            var mine = book?.notables.Where(n => n != null && n.town == id).ToList() ?? new List<Notable>();
            // A town gone from the world: what the ledger kept of it, else what the notables and the news knew.
            if (s == null)
            {
                var rec = TownRecord(id);
                var n0 = mine.FirstOrDefault(n => !n.townName.NullOrEmpty());
                var h0 = outs?.heard.FirstOrDefault(h => h != null && h.town == id);
                v.name = rec?.name ?? n0?.townName ?? h0?.townName;
                v.people = rec?.people ?? mine.FirstOrDefault(n => n.faction != null)?.faction ?? h0?.faction;
                if (v.name == null && v.people == null && mine.Count == 0) return null;
            }
            v.peopleRef = Refs.Of(v.people);
            foreach (var n in mine.OrderBy(n => (int)n.role).ThenBy(n => n.id))
            {
                var nv = new NotableView
                {
                    person = n.person, id = n.id, pawn = n.pawn, name = n.Name, role = n.role, title = NotableBook.TitleOf(n), alive = n.Alive,
                    opinion = NotableBook.Opinion(n)
                };
                v.notables.Add(nv);
                if (v.head == null && n.role == NotableRole.Head && n.Alive) v.head = nv;
            }
            var stance = StanceOf(v.people);
            v.overall = v.head != null ? stance.ByHead(v.head.opinion) : stance;
            v.mood = PerColony(v.overall);
            v.events = Events(v.people, v.name);
            if (outs != null)
                foreach (var h in outs.heard.Where(h => h != null && h.town == id).OrderByDescending(h => h.tick))
                    v.heard.Add(new HeardView { text = h.text, teller = h.teller, told = h.told, tick = h.tick });
            return v;
        }

        /// <summary>
        /// The world situations touching a people or a town: running ones about them, against them or placed at the town
        /// (by name, as the situations name their place), and those about them ended in the last fifteen days.
        /// </summary>
        private static List<EventView> Events(Faction f, string townName)
        {
            var list = new List<EventView>();
            var ws = WorldSituations.Instance;
            if (ws == null) return list;
            foreach (var s in ws.Running)
            {
                if (s?.def == null || s.ctx == null) continue;
                bool theirs = f != null && s.ctx.faction == f, against = f != null && s.ctx.enemy == f;
                bool here = !townName.NullOrEmpty() && s.ctx.placeName == townName;
                if (!theirs && !against && !here) continue;
                list.Add(new EventView
                {
                    id = s.id, defName = s.def.defName, label = s.Label, running = true, theirs = theirs, against = against, start = s.startTick, end = s.endTick,
                    helped = s.help > 0
                });
            }
            if (f != null)
            {
                int now = Now;
                foreach (var r in ws.history)
                {
                    if (r == null || r.faction != f || r.endTick <= 0 || (now - r.endTick) / (float)GenDate.TicksPerDay > EndedLatelyDays) continue;
                    list.Add(new EventView { defName = r.defName, label = r.Called, theirs = true, start = r.startTick, end = r.endTick, helped = r.helped });
                }
            }
            return list;
        }

        // ---------------- A people ----------------

        /// <summary>A people of the world (not the player's own): null without one.</summary>
        public static PeopleView People(Faction f) => f == null || f.IsPlayer ? null : Kept(peoples, f.loadID, () => BuildPeople(f), "a people's view");

        private static PeopleView BuildPeople(Faction f)
        {
            var v = new PeopleView
            {
                faction = f, people = Refs.Of(f), name = f.Name, age = LedgerMath.AgeOf((int)(f.def?.techLevel ?? TechLevel.Industrial)),
                empire = f.def != null && f.def.HasRoyalTitles
            };
            v.overall = StanceOf(f);
            v.stance = PerColony(v.overall);
            var mem = FactionMemory.Instance;
            if (mem != null && FactionMemory.On)
            {
                int now = Now;
                foreach (var d in mem.Of(f).Where(d => d != null).OrderByDescending(d => d.StrengthAt(now)))
                {
                    v.remembers.Add(new MemoryView
                    {
                        id = d.id, kind = d.kind, side = d.side, weight = d.weight, strength = d.StrengthAt(now), tick = d.tick, first = d.first, count = d.count,
                        name = d.name, settled = d.settled, by = d.by, byName = d.byName
                    });
                }
            }
            var outs = OutsiderBook.Instance;
            if (outs != null)
                foreach (var w in outs.wars.Where(w => w != null && w.Of(f)).OrderByDescending(w => w.start))
                {
                    var other = w.Other(f);
                    v.wars.Add(new WarView { other = other, otherRef = Refs.Of(other), start = w.start, end = w.end, fought = w.fought });
                }
            return v;
        }

        // ---------------- A person ----------------

        /// <summary>
        /// Someone out in the world, as the stores know them: a notable, an outsider someone talked with, a regular, a
        /// departed ex-colonist, or anyone else the game still has a pawn for. Null for a person nobody knows.
        /// </summary>
        public static PersonView Person(Ref r) => r.kind != RefKind.Person ? null : Kept(personViews, r.id, () => BuildPerson(r, null), "a person's view");

        /// <summary>The same, for a pawn in hand (no search for it).</summary>
        public static PersonView Person(Pawn p) => p == null ? null : Kept(personViews, p.thingIDNumber, () => BuildPerson(Refs.Of(p), p), "a person's view");

        private static PersonView BuildPerson(Ref r, Pawn given)
        {
            if (r.IsNone) return null;
            var book = NotableBook.Instance;
            var outs = OutsiderBook.Instance;
            var hap = CouncilGameComponent.Instance?.happenings;
            // The ledger's own record first: every notable is a person there, and so is anyone a tie names.
            var rec = PersonRecord(r);
            bool Is(Pawn p) => p != null && r.HasPawn && p.thingIDNumber == r.id;
            var n = book?.notables.FirstOrDefault(x => x != null && x.person == r) ?? book?.notables.FirstOrDefault(x => x != null && Is(x.pawn));
            var pairs = outs?.pairs.Where(x => x != null && Is(x.outsider)).OrderByDescending(x => x.lastTick).ToList() ?? new List<OutsiderPair>();
            var reg = hap?.regulars.FirstOrDefault(x => x != null && Is(x.pawn));
            var dep = hap?.departed.FirstOrDefault(x => x != null && Is(x.pawn));
            var pawn = given ?? rec?.pawn ?? n?.pawn ?? pairs.FirstOrDefault()?.outsider ?? reg?.pawn ?? dep?.pawn;
            if (pawn == null && rec == null && n == null && pairs.Count == 0 && reg == null && dep == null) pawn = Refs.Pawn(r);
            if (pawn == null && rec == null && n == null && pairs.Count == 0 && reg == null && dep == null) return null;
            var v = new PersonView { person = r, pawn = pawn };
            v.name = (pawn != null && !pawn.Discarded ? pawn.LabelShort : null) ?? rec?.name ?? n?.Name ?? pairs.FirstOrDefault()?.outsiderName ?? dep?.name ?? "?";
            v.people = n?.faction ?? (pawn != null ? Outsiders.PeopleOf(pawn) : null) ?? rec?.people ?? reg?.faction ?? pairs.FirstOrDefault()?.faction;
            v.alive = pawn != null ? !pawn.Dead && !pawn.Discarded : n != null && n.Alive;
            if (n != null)
            {
                v.notable = n.id;
                v.town = n.town;
                v.townName = n.townName;
                v.role = n.role.ToString().ToLowerInvariant();
                v.title = NotableBook.TitleOf(n);
            }
            else
            {
                var t = pawn != null && v.people != null ? Outsiders.TownOf(pawn) : null;
                if (t != null)
                {
                    v.town = t.ID;
                    v.townName = t.Name;
                }
                v.role = reg != null ? "regular" : dep != null ? "departed" : pairs.Count > 0 ? "visitor" : null;
            }
            v.trade = reg?.kind;
            Met(v, n, pairs, reg, dep);
            return v;
        }

        /// <summary>Who of the colony knows them and how: the notables' record first, then outsider pairs, regulars, departed ex-colonists.</summary>
        private static void Met(PersonView v, Notable n, List<OutsiderPair> pairs, Regular reg, Departed dep)
        {
            var seen = new HashSet<string>();
            void Add(Pawn c, string name, string how, int tick)
            {
                string key = c != null ? "p" + c.thingIDNumber : "n" + (name ?? "");
                if (c == null && name.NullOrEmpty() || !seen.Add(key)) return;
                v.metBy.Add(new MetView { colonist = c, name = c != null && !c.Discarded ? c.LabelShort : name, how = how, tick = tick });
            }
            if (n != null)
            {
                Add(n.metBy, n.metByName, n.met ?? "notable", n.since);
                foreach (var r in n.regards.Where(r => r != null).OrderByDescending(r => r.personal)) Add(r.pawn, r.name, "letters", -1);
            }
            foreach (var p in pairs) Add(p.colonist, p.colonistName, p.friends ? "friend" : "talked", p.lastTick);
            if (reg != null) Add(reg.friendOf, null, "regular", reg.lastTick);
            if (dep != null) Add(null, Faction.OfPlayer?.Name, "colonist", dep.tick);
        }
    }
}
