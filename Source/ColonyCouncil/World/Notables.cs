using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>One thing a notable remembers of the colony: when, what, and how it moved them.</summary>
    public class NotableNote : IExposable
    {
        public int tick;
        public string text;
        /// <summary>How much better (or worse) they thought of the colony for it.</summary>
        public float shift;

        public void ExposeData()
        {
            Scribe_Values.Look(ref tick, "tick");
            Scribe_Values.Look(ref text, "text");
            Scribe_Values.Look(ref shift, "shift");
        }
    }

    /// <summary>What passed between a notable and one colonist in person (letters, a call, a visit), fading slowly.</summary>
    public class NotableRegard : IExposable
    {
        public Pawn pawn;
        public string name;
        public float personal;

        public void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving && pawn != null && pawn.Discarded) pawn = null;
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Values.Look(ref name, "name");
            Scribe_Values.Look(ref personal, "personal");
        }
    }

    /// <summary>A loss the colony knows of among a people (one of their notables dead), for a letter of condolence.</summary>
    public class NotableLoss : IExposable
    {
        public Faction faction;
        public int town;
        public string name;
        public string role;
        public int tick;

        public void ExposeData()
        {
            Scribe_References.Look(ref faction, "faction");
            Scribe_Values.Look(ref town, "town");
            Scribe_Values.Look(ref name, "name");
            Scribe_Values.Look(ref role, "role");
            Scribe_Values.Look(ref tick, "tick");
        }
    }

    /// <summary>A visitor or trader seen at the colony, who becomes a face the colony knows once they're home.</summary>
    public class SeenVisitor : IExposable
    {
        public Pawn pawn;
        public string met;
        /// <summary>The colonist who met them, if one did.</summary>
        public Pawn by;
        public int tick;

        public void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                if (pawn != null && pawn.Discarded) pawn = null;
                if (by != null && by.Discarded) by = null;
            }
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Values.Look(ref met, "met");
            Scribe_References.Look(ref by, "by");
            Scribe_Values.Look(ref tick, "tick");
        }
    }

    /// <summary>
    /// A named person beyond the walls: a real pawn kept in the world while they matter, with their town and role, how
    /// they regard the colony and some of its people, and what passed between them (see <see cref="NotableBook"/>).
    /// </summary>
    public class Notable : IExposable
    {
        public int id;
        /// <summary>
        /// Who they are in the world ledger: their pawn's ref, or a ledger number for someone known only by name. The
        /// ledger keeps their pawn (while the game does) and their name; the notable keeps its role and town.
        /// </summary>
        public Ref person;
        public Faction faction;
        /// <summary>Their town (the settlement's world object ID; an acquaintance's is their people's town nearest the colony).</summary>
        public int town = -1;
        public string townName;
        public NotableRole role;
        /// <summary>How the colony met them, for an acquaintance: freed, tended, camp, guest, trader, visitor, envoy.</summary>
        public string met;
        /// <summary>The colonist they met (who freed them, who hosted them).</summary>
        public Pawn metBy;
        public string metByName;
        /// <summary>Made by Rimfolk for their role (let go when no longer needed); else someone the game or another part keeps.</summary>
        public bool made;
        /// <summary>
        /// What passed between them and the colony in person: a Regard tie in the world ledger, from them to the colony,
        /// its score the tie's number (none while it's nothing). It fades daily from <see cref="faded"/>, as before.
        /// </summary>
        public float personal
        {
            get => RegardTie(false)?.terms.number ?? 0f;
            set
            {
                var t = RegardTie(value != 0f);
                if (t == null || t.terms.number == value) return;
                t.terms.number = value;
                WorldLedger.Touch(t);
            }
        }

        /// <summary>When what passed between them last faded.</summary>
        public int faded;

        /// <summary>The origin of the ledger's regard ties for a notable's own regard of the colony (the notable's id in its count).</summary>
        public const string RegardOrigin = "notable";

        [Unsaved] private Tie regard;
        [Unsaved] private int regardLooked = -1;
        /// <summary>An older save's regard, read on load and moved into the ledger (NotableBook.FinalizeInit).</summary>
        private float oldPersonal;

        /// <summary>Their regard tie, found once (looked for again when the ledger changes), made when asked to.</summary>
        private Tie RegardTie(bool make)
        {
            if (regard != null && regard.Live && regard.from == person) return regard;
            regard = null;
            if (regardLooked != WorldLedger.Version || make)
            {
                var me = Person;
                foreach (var t in WorldLedger.OwnTies(Tie.Kind.Regard))
                    if (t.Live && t.origin == RegardOrigin && t.from == me && t.terms.count == id)
                    {
                        regard = t;
                        break;
                    }
                if (regard == null && make && !me.IsNone)
                    regard = WorldLedger.AddTie(Tie.Kind.Regard, me, Refs.Us, new TieTerms { count = id }, origin: RegardOrigin);
                regardLooked = WorldLedger.Version;
            }
            return regard;
        }

        /// <summary>Their regard is over (they're no longer a notable, or the record went to someone else).</summary>
        public void EndRegard()
        {
            var t = RegardTie(false);
            if (t != null) WorldLedger.Set(t, Tie.State.Ended);
            regard = null;
        }

        /// <summary>An older save's regard goes into the ledger.</summary>
        public void MoveRegard()
        {
            if (oldPersonal == 0f) return;
            personal = oldPersonal;
            oldPersonal = 0f;
        }
        public List<NotableRegard> regards = new List<NotableRegard>();
        public List<NotableNote> notes = new List<NotableNote>();
        /// <summary>The last letter for each purpose (by name), for the wait between letters.</summary>
        public List<string> lastKeys = new List<string>();
        public List<int> lastTicks = new List<int>();
        /// <summary>Threats warned of and losses mourned already (each once).</summary>
        public List<string> done = new List<string>();
        public int since;

        /// <summary>An older save's pawn and name, read once and handed to the ledger.</summary>
        private Pawn oldPawn;
        private string oldName;
        [Unsaved] private LedgerPerson record;

        /// <summary>The ledger's record of them (found once, then kept).</summary>
        private LedgerPerson Record
        {
            get
            {
                if (record == null || record.person != person)
                {
                    if (person.IsNone) HandOver();
                    record = WorldLedger.PersonRecord(person);
                }
                return record;
            }
        }

        /// <summary>Their pawn, as the ledger keeps it (null once the game let them go).</summary>
        public Pawn pawn => Record?.pawn ?? oldPawn;

        /// <summary>Their name, kept for when they're gone.</summary>
        public string name => Record?.name ?? oldName;

        /// <summary>
        /// An older save's notable becomes a person of the ledger: their pawn's, or a ledger number for one known only by
        /// name. Done on load; if the ledger wasn't there yet, the first time they're asked about.
        /// </summary>
        private void HandOver()
        {
            if (!person.IsNone || oldPawn == null && oldName.NullOrEmpty() || WorldLedger.Instance == null) return;
            person = oldPawn != null ? WorldLedger.PersonOf(oldPawn) : WorldLedger.NewPerson(oldName, faction);
            var rec = WorldLedger.PersonRecord(person);
            if (rec == null) return;
            if (!oldName.NullOrEmpty()) rec.name = oldName;
            oldPawn = null;
            oldName = null;
        }

        /// <summary>Who they are in the world ledger (an older save's notable handed over first, if it wasn't yet).</summary>
        public Ref Person
        {
            get
            {
                if (person.IsNone) HandOver();
                return person;
            }
        }

        /// <summary>The record is someone's from now on: the ledger's person for this pawn.</summary>
        public void SetPerson(Pawn p)
        {
            person = WorldLedger.PersonOf(p);
            record = null;
        }

        public bool Alive => pawn != null && !pawn.Dead && !pawn.Discarded;

        public string Name => pawn != null && !pawn.Discarded ? pawn.LabelShort : name ?? "?";

        public int Last(LetterPurpose p)
        {
            int i = lastKeys.IndexOf(p.ToString());
            return i >= 0 && i < lastTicks.Count ? lastTicks[i] : -1;
        }

        public void SetLast(LetterPurpose p, int tick)
        {
            int i = lastKeys.IndexOf(p.ToString());
            if (i >= 0 && i < lastTicks.Count) lastTicks[i] = tick;
            else
            {
                lastKeys.Add(p.ToString());
                lastTicks.Add(tick);
            }
        }

        public NotableRegard RegardOf(Pawn p, bool make)
        {
            if (p == null) return null;
            var r = regards.FirstOrDefault(x => x.pawn == p);
            if (r == null && make)
            {
                r = new NotableRegard { pawn = p, name = p.LabelShort };
                regards.Add(r);
                while (regards.Count > NotableMath.MaxRegards)
                    regards.Remove(regards.Where(x => x != r).OrderBy(x => x.pawn == null || x.pawn.Dead ? 0 : 1).ThenBy(x => Mathf.Abs(x.personal)).First());
            }
            return r;
        }

        public void Remember(string text, float shift)
        {
            if (text.NullOrEmpty()) return;
            notes.Add(new NotableNote { tick = Find.TickManager.TicksGame, text = text, shift = shift });
            while (notes.Count > NotableMath.MaxMemory) notes.RemoveAt(0);
            NotableBook.Changed();
        }

        public void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                if (oldPawn != null && oldPawn.Discarded) oldPawn = null;
                if (metBy != null && metBy.Discarded) metBy = null;
                regards.RemoveAll(r => r == null || r.pawn == null && r.name.NullOrEmpty());
            }
            Scribe_Values.Look(ref id, "id");
            Refs.Look(ref person, "person");
            // An older save kept the pawn and the name here (saved again only if they couldn't be handed to the ledger).
            if (Scribe.mode != LoadSaveMode.Saving || person.IsNone)
            {
                Scribe_References.Look(ref oldPawn, "pawn", true);
                Scribe_Values.Look(ref oldName, "name");
            }
            Scribe_References.Look(ref faction, "faction");
            Scribe_Values.Look(ref town, "town", -1);
            Scribe_Values.Look(ref townName, "townName");
            Scribe_Values.Look(ref role, "role");
            Scribe_Values.Look(ref met, "met");
            Scribe_References.Look(ref metBy, "metBy");
            Scribe_Values.Look(ref metByName, "metByName");
            Scribe_Values.Look(ref made, "made");
            // An older save kept the regard here; it's the ledger's now.
            if (Scribe.mode != LoadSaveMode.Saving) Scribe_Values.Look(ref oldPersonal, "personal");
            Scribe_Values.Look(ref faded, "faded");
            Scribe_Collections.Look(ref regards, "regards", LookMode.Deep);
            Scribe_Collections.Look(ref notes, "notes", LookMode.Deep);
            Scribe_Collections.Look(ref lastKeys, "lastKeys", LookMode.Value);
            Scribe_Collections.Look(ref lastTicks, "lastTicks", LookMode.Value);
            Scribe_Collections.Look(ref done, "done", LookMode.Value);
            Scribe_Values.Look(ref since, "since");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                HandOver();
                record = null;
                regards ??= new List<NotableRegard>();
                regards.RemoveAll(r => r == null);
                notes ??= new List<NotableNote>();
                notes.RemoveAll(n => n == null);
                lastKeys ??= new List<string>();
                lastTicks ??= new List<int>();
                if (lastKeys.Count != lastTicks.Count)
                {
                    lastKeys.Clear();
                    lastTicks.Clear();
                }
                done ??= new List<string>();
            }
        }
    }

    /// <summary>
    /// The colony's neighbours by name. For the towns within reach of the colony (the nearest few, and any it has dealt
    /// with) a few named people each: the town's head (their people's own leader at their seat, else a headman, reeve,
    /// mayor or administrator by their era), a likely heir, an envoy and a known trader, made with their people's own
    /// kinds and kept in the world while they matter; and the faces the colony met who went home (a prisoner set free, a
    /// wounded guest tended, a camp's leader, a guest of rank, a visiting trader). Each regards the colony and some of its
    /// people (from their people's goodwill and memory, their own nature, and what passed between them), remembers what
    /// passed, ages, and can die or be followed by someone new. Letters and calls to them: World/NeighbourLetters.cs.
    /// Its own game component; the hour's pass at <see cref="Sched.Neighbours"/>.
    /// </summary>
    public class NotableBook : GameComponent
    {
        public List<Notable> notables = new List<Notable>();
        public List<NeighbourLetter> letters = new List<NeighbourLetter>();
        public List<NotableLoss> losses = new List<NotableLoss>();
        public List<SeenVisitor> seen = new List<SeenVisitor>();
        public int nextId = 1;
        public int nextLetterId = 1;
        /// <summary>The last day old age was rolled for.</summary>
        public int agedDay = -1;

        private static NotableBook cached;
        private static Game cachedGame;
        private static int version;

        /// <summary>Bumped on every change (the panels rebuild when it moves).</summary>
        public static int Version => version;

        public static void Changed() => version++;

        public NotableBook(Game game)
        {
        }

        public static NotableBook Instance
        {
            get
            {
                var game = Current.Game;
                if (game == null) return null;
                if (cachedGame != game || cached == null)
                {
                    cached = game.GetComponent<NotableBook>();
                    cachedGame = game;
                }
                return cached;
            }
        }

        public static bool On => (ColonyCouncilMod.Settings?.notables ?? true) && Instance != null;

        private static int Now => Find.TickManager.TicksGame;

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            cached = this;
            cachedGame = Current.Game;
            // An older save's regard of each notable goes into the world ledger.
            try
            {
                foreach (var n in notables) n?.MoveRegard();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("moving an older save's notables' regard into the world ledger", e);
            }
            Changed();
        }

        public Notable Get(int id) => notables.FirstOrDefault(n => n.id == id);

        /// <summary>Regard ties of notables the book no longer has (dropped without a word, or an edited save): ended.</summary>
        private void EndStrayRegard()
        {
            var mine = new HashSet<(Ref, int)>(notables.Where(n => n != null).Select(n => (n.person, n.id)));
            foreach (var t in WorldLedger.OwnTies(Tie.Kind.Regard).Where(t => t.Live && t.origin == Notable.RegardOrigin).ToList())
                if (!mine.Contains((t.from, t.terms.count)))
                    WorldLedger.Set(t, Tie.State.Ended);
        }

        // ---------------- The hour ----------------

        public override void GameComponentTick()
        {
            int now = Now;
            if (letters.Count > 0 && Sched.Every(now, Sched.RareTicks, Sched.RareNeighbours) && NeighbourLetters.WantsRare(this))
                using (PassClock.Time(Pass.RareNeighbours))
                    Safe("rare", () => NeighbourLetters.Rare(this, now));
            if (!Sched.Hourly(now, Sched.Neighbours)) return;
            using var clock = PassClock.Time(Pass.Neighbours);
            if (!(ColonyCouncilMod.Settings?.notables ?? true))
            {
                if (notables.Count > 0) Safe("let go", LetAllGo);
                return;
            }
            Safe("seen", () => Seen(now));
            Safe("keep", Keep);
            // The towns and their people are looked over every six hours (and at once in a new game).
            if (now / Sched.HourTicks % 6 == 0 || notables.Count == 0) Safe("towns", () => Refresh(now));
            if (Sched.FirstHourOfDay(now)) Safe("day", () => Daily(now));
            Safe("letters", () => NeighbourLetters.Hourly(this, now));
        }

        private static void Safe(string what, Action a)
        {
            try
            {
                a();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("notables " + what, e);
            }
        }

        // ---------------- The towns within reach ----------------

        private static Map Home => HappeningTracker.HomeMap();

        /// <summary>A town of a people that can be written to: theirs, standing, named, on the colony's own layer of the world.</summary>
        private static bool TownFits(Settlement s, Map map) =>
            s != null && !s.Destroyed && s.HasName && FactionMemory.Remembers(s.Faction) && !s.Faction.defeated && (map == null || s.Tile.Layer == map.Tile.Layer);

        public static Settlement TownOf(int id)
        {
            if (id < 0) return null;
            foreach (var s in Find.WorldObjects.Settlements)
                if (s.ID == id)
                    return s;
            return null;
        }

        /// <summary>The people's seat: their oldest standing town (their own leader is its head).</summary>
        public static Settlement SeatOf(Faction f)
        {
            Settlement best = null;
            foreach (var s in Find.WorldObjects.Settlements)
                if (s.Faction == f && !s.Destroyed && (best == null || s.ID < best.ID))
                    best = s;
            return best;
        }

        /// <summary>The towns the colony counts as neighbours now, nearest first.</summary>
        public List<Settlement> Towns(Map map)
        {
            var all = Find.WorldObjects.Settlements.Where(s => TownFits(s, map)).ToList();
            if (map == null || all.Count == 0) return new List<Settlement>();
            var tiles = all.Select(s => LivingEvents.Distance(map.Tile, s.Tile)).ToList();
            // Each people's town nearest the colony: where those who remember the colony live.
            var nearest = new Dictionary<Faction, int>();
            for (int i = 0; i < all.Count; i++)
                if (!nearest.TryGetValue(all[i].Faction, out int j) || tiles[i] < tiles[j])
                    nearest[all[i].Faction] = i;
            var mem = FactionMemory.Instance;
            var remembering = mem != null && FactionMemory.On ? new HashSet<Faction>(mem.deeds.Where(d => d?.faction != null).Select(d => d.faction)) : new HashSet<Faction>();
            var dealt = all.Select((s, i) => notables.Any(n => n.town == s.ID && n.notes.Count > 0)
                                             || remembering.Contains(s.Faction) && nearest.TryGetValue(s.Faction, out int k) && k == i).ToList();
            return NotableMath.InReach(tiles, dealt).Select(i => all[i]).ToList();
        }

        private void Refresh(int now)
        {
            var map = Home;
            if (map == null) return;
            var towns = Towns(map);
            var ids = new HashSet<int>(towns.Select(s => s.ID));
            // The gone are followed (the heir steps up), the faded let go.
            foreach (var n in notables.ToList()) Look(n, ids, now);
            int made = 0;
            foreach (var s in towns)
            {
                if (made >= NotableMath.MakePerHour) break;
                made += Staff(s, now, NotableMath.MakePerHour - made);
            }
            // Acquaintances live in their people's town nearest the colony.
            foreach (var n in notables.Where(n => n.role == NotableRole.Acquaintance && n.Alive))
            {
                var t = TownOf(n.town);
                if (t != null && t.Faction == n.faction) continue;
                var near = WorldSituationWorker.NearestTown(n.pawn.Faction ?? n.faction, map);
                if (near == null) continue;
                n.faction = near.Faction;
                n.town = near.ID;
                n.townName = near.Name;
            }
            NoteTowns();
            Changed();
        }

        /// <summary>The towns the notables live in, noted in the ledger (kept there for when a town is gone).</summary>
        private void NoteTowns()
        {
            foreach (var n in notables)
                if (n != null && n.town >= 0)
                    WorldLedger.NoteTown(n.town, n.townName, n.faction);
        }

        /// <summary>One notable: still there and still theirs? Gone: remembered as a loss, and followed in their role.</summary>
        private void Look(Notable n, HashSet<int> towns, int now)
        {
            var town = TownOf(n.town);
            bool inReach = n.role == NotableRole.Acquaintance || towns.Contains(n.town);
            // The seat's head is their people's own leader: whoever the game has made leader now.
            if (n.role == NotableRole.Head && !n.made && town != null && n.faction != null && n.faction.leader != n.pawn)
            {
                var old = n.pawn;
                if (old != null && old.Dead) Lost(n, now);
                var next = n.faction.leader;
                if (next != null && !next.Dead)
                {
                    // Someone the colony knew already (the heir it knew by name, a face it met): their own record stays.
                    var known = notables.FirstOrDefault(o => o != n && o.pawn == next);
                    if (known != null)
                    {
                        known.role = NotableRole.Head;
                        known.town = n.town;
                        known.townName = n.townName;
                        known.made = false;
                        known.Remember(Old(n), 0f);
                        Drop(n);
                        return;
                    }
                    // Letters to the one who led are for them, not their follower.
                    foreach (var l in letters.Where(l => l.Open && l.notable == n.id).ToList()) NeighbourLetters.Cancel(l, "CC_Letter_Cancel_Gone");
                    Replace(n, next, false);
                    n.Remember(Old(n), 0f);
                }
                else Drop(n);
                return;
            }
            bool gone = n.pawn == null || n.pawn.Discarded || n.pawn.Dead || n.pawn.Faction != n.faction
                        || n.pawn.IsPrisonerOfColony || n.pawn.IsSlaveOfColony || n.faction == null || n.faction.defeated;
            if (gone)
            {
                if (n.pawn != null && n.pawn.Dead) Lost(n, now);
                // A head is followed by the heir; the heir's place, the envoy's and the trader's are filled later.
                if (n.role == NotableRole.Head && town != null && inReach)
                {
                    var heir = notables.FirstOrDefault(h => h.town == n.town && h.role == NotableRole.Heir && h.Alive && h.pawn.Faction == n.faction);
                    if (NotableMath.HeirSucceeds(n.role, heir != null))
                    {
                        heir.role = NotableRole.Head;
                        heir.Remember("CC_Notable_Note_Succeeded".Translate(n.Name.Named("NAME"), TitleOf(heir).Named("TITLE"), (heir.townName ?? "").Named("TOWN")).Resolve(), 0f);
                        if (n.notes.Count > 0 || heir.notes.Count > 0)
                            Notify.Message("CC_Notable_Msg_Succeeded".Translate(heir.Name.Named("NAME"), n.Name.Named("OLD"), TitleOf(heir).Named("TITLE"), (heir.townName ?? "").Named("TOWN")).Resolve(),
                                LookTargets.Invalid, MessageTypeDefOf.NeutralEvent, Importance.Notable);
                    }
                }
                Drop(n);
                return;
            }
            // Out of reach (the town gone, the colony moved, the people fallen out of the world): let go, unless something
            // passed between them in the last season.
            if (!inReach || town == null && n.role != NotableRole.Acquaintance)
            {
                bool recent = n.notes.Any(x => now - x.tick < GenDate.TicksPerDay * 60);
                if (!recent) Drop(n);
            }
        }

        private static string Old(Notable n) => "CC_Notable_Note_NewLeader".Translate((n.faction?.leader?.LabelShort ?? "?").Named("NAME"), (n.faction?.Name ?? "").Named("FACTION")).Resolve();

        /// <summary>The seat's head is someone else now: the record goes to them, with nothing of the old one's memory.</summary>
        private static void Replace(Notable n, Pawn p, bool made)
        {
            n.EndRegard();
            n.SetPerson(p);
            n.made = made;
            n.personal = 0f;
            n.regards.Clear();
            n.notes.Clear();
            n.lastKeys.Clear();
            n.lastTicks.Clear();
            n.done.Clear();
            n.since = Now;
        }

        /// <summary>A death the colony knows of, kept a while for a letter of condolence.</summary>
        private void Lost(Notable n, int now)
        {
            if (n.faction == null) return;
            losses.Add(new NotableLoss { faction = n.faction, town = n.town, name = n.Name, role = TitleOf(n), tick = now });
            while (losses.Count > 12) losses.RemoveAt(0);
        }

        /// <summary>The towns' roles, filled where empty; how many were made.</summary>
        private int Staff(Settlement s, int now, int budget)
        {
            int made = 0;
            var f = s.Faction;
            bool seat = SeatOf(f) == s;
            foreach (NotableRole role in new[] { NotableRole.Head, NotableRole.Heir, NotableRole.Envoy, NotableRole.Trader })
            {
                if (made >= budget) break;
                if (notables.Any(n => n.town == s.ID && n.role == role)) continue;
                if (role == NotableRole.Head && seat && f.leader != null && !f.leader.Dead && !f.leader.Discarded)
                {
                    // Their leader, known already as a face the colony met, is the seat's head from now on.
                    var known = notables.FirstOrDefault(n => n.pawn == f.leader);
                    if (known != null)
                    {
                        known.role = NotableRole.Head;
                        known.town = s.ID;
                        known.townName = s.Name;
                        known.faction = f;
                    }
                    else Add(f.leader, s, role, false, null, null);
                    continue;
                }
                var head = notables.FirstOrDefault(n => n.town == s.ID && n.role == NotableRole.Head && n.Alive);
                if (role == NotableRole.Heir && head == null) continue;
                var p = Make(f, role, head?.pawn);
                made++;
                if (p != null) Add(p, s, role, true, null, null);
            }
            return made;
        }

        private Notable Add(Pawn p, Settlement s, NotableRole role, bool made, string met, Pawn by)
        {
            var n = new Notable
            {
                id = nextId++, faction = s?.Faction ?? p.Faction, town = s?.ID ?? -1, townName = s?.Name, role = role, made = made,
                met = met, metBy = by, metByName = by?.LabelShort, since = Now, faded = Now
            };
            n.SetPerson(p);
            notables.Add(n);
            if (s != null) WorldLedger.NoteTown(s.ID, s.Name, s.Faction);
            if (!p.Spawned && !made && Find.WorldPawns.Contains(p) && !PawnUtility.IsFactionLeader(p)) Find.WorldPawns.ForcefullyKeptPawns.Add(p);
            Changed();
            return n;
        }

        /// <summary>Someone of their people for a role: made with their own kinds, of an age that fits it, kept in the world.</summary>
        private static Pawn Make(Faction f, NotableRole role, Pawn head)
        {
            try
            {
                PawnKindDef kind = null;
                // A town's head is of their leaders' kind (never for a people of royal titles: that would make a second ruler).
                if (role == NotableRole.Head && !f.def.HasRoyalTitles) kind = f.def.fixedLeaderKinds?.Where(k => k?.RaceProps?.Humanlike == true).RandomElementWithFallback();
                if (role == NotableRole.Trader) kind = TraderKind(f);
                if (role == NotableRole.Heir && head?.kindDef?.RaceProps?.Humanlike == true) kind = head.kindDef;
                kind ??= Arrivals.PersonKind(f);
                if (kind == null) return null;
                FloatRange age;
                switch (role)
                {
                    case NotableRole.Head: age = new FloatRange(40f, 68f); break;
                    case NotableRole.Heir: age = new FloatRange(18f, 34f); break;
                    case NotableRole.Envoy: age = new FloatRange(25f, 55f); break;
                    default: age = new FloatRange(25f, 60f); break;
                }
                string last = role == NotableRole.Heir && head?.Name is NameTriple tri ? tri.Last : null;
                var req = new PawnGenerationRequest(kind, f, PawnGenerationContext.NonPlayer, forceGenerateNewPawn: true, canGeneratePawnRelations: false,
                    colonistRelationChanceFactor: 0f, fixedLastName: last, biologicalAgeRange: age, developmentalStages: DevelopmentalStage.Adult, dontGiveWeapon: true);
                var p = PawnGenerator.GeneratePawn(req);
                if (p == null) return null;
                Find.WorldPawns.PassToWorld(p, PawnDiscardDecideMode.KeepForever);
                return p;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("notable made " + f?.def?.defName, e);
                return null;
            }
        }

        /// <summary>The kind of trader a people's caravans bring, or null.</summary>
        private static PawnKindDef TraderKind(Faction f) =>
            f?.def?.pawnGroupMakers?.Where(g => g != null && g.kindDef == PawnGroupKindDefOf.Trader && g.traders != null).SelectMany(g => g.traders)
                .Select(o => o?.kind).Where(k => k?.RaceProps?.Humanlike == true).RandomElementWithFallback();

        /// <summary>Let go of a notable: someone Rimfolk made goes back to nothing, anyone else back to the world's own keeping.</summary>
        private void Drop(Notable n)
        {
            notables.Remove(n);
            n.EndRegard();
            var p = n.pawn;
            if (p != null && !p.Discarded && !notables.Any(x => x.pawn == p) && !letters.Any(l => l.Open && l.notable == n.id))
            {
                if (n.made && !p.Dead && WorldSituationWorker.FreeInWorld(p) && !PawnUtility.IsFactionLeader(p) && !HeldElsewhere(p) && !HasTies(p)) Arrivals.Discard(p);
                else if (!p.Spawned && !PawnUtility.IsFactionLeader(p) && !HeldElsewhere(p)) Arrivals.Release(p);
            }
            foreach (var l in letters.Where(l => l.Open && l.notable == n.id)) NeighbourLetters.Cancel(l, "CC_Letter_Cancel_Gone");
            Changed();
        }

        /// <summary>Someone of the colony's own is family to them (then the world's own clean-up decides, not Rimfolk).</summary>
        private static bool HasTies(Pawn p) => p.relations != null && p.relations.DirectRelations.Any(r => r.otherPawn != null && r.otherPawn.Faction == Faction.OfPlayer);

        /// <summary>Another part of Rimfolk keeps them in the world (a camp that may come back, a world event, an event waiting).</summary>
        private static bool HeldElsewhere(Pawn p)
        {
            if (CampReturns.Past.Any(c => c.people.Contains(p))) return true;
            if (WorldSituations.Holds(p)) return true;
            return CouncilGameComponent.Instance?.happenings?.Holds(p) ?? false;
        }

        private void LetAllGo()
        {
            foreach (var n in notables.ToList()) Drop(n);
            foreach (var l in letters.Where(l => l.Open).ToList()) NeighbourLetters.Cancel(l, null);
            letters.Clear();
        }

        /// <summary>Those out in the world stay kept while they're notables (another part letting them go doesn't drop them).</summary>
        private void Keep()
        {
            foreach (var n in notables)
            {
                var p = n.pawn;
                if (p == null || p.Dead || p.Discarded || p.Spawned || PawnUtility.IsFactionLeader(p) || !Find.WorldPawns.Contains(p)) continue;
                Find.WorldPawns.ForcefullyKeptPawns.Add(p);
            }
        }

        // ---------------- Once a day ----------------

        private void Daily(int now)
        {
            int day = now / GenDate.TicksPerDay;
            if (day == agedDay) return;
            agedDay = day;
            foreach (var n in notables)
            {
                // What passed between them fades a little.
                float days = (now - n.faded) / (float)GenDate.TicksPerDay;
                if (days > 0f)
                {
                    n.personal = NotableMath.Fade(n.personal, days);
                    foreach (var r in n.regards) r.personal = NotableMath.Fade(r.personal, days);
                    n.faded = now;
                }
                n.regards.RemoveAll(r => r.pawn == null || r.pawn.Dead || r.pawn.Discarded || Mathf.Abs(r.personal) < 0.5f && r.pawn != n.metBy);
                // Old age, for those Rimfolk keeps (never a people's leader: the game's own).
                var p = n.pawn;
                if (n.made && n.Alive && !p.Spawned && WorldSituationWorker.FreeInWorld(p) && !PawnUtility.IsFactionLeader(p)
                    && Rand.Chance(NotableMath.DeathChancePerDay(Flavor.HumanAge(p))))
                    WorldSituationWorker.DieOffscreen(p);
            }
            losses.RemoveAll(l => l == null || l.faction == null || now - l.tick > GenDate.TicksPerDay * 30);
            seen.RemoveAll(s => s.pawn == null || s.pawn.Discarded || s.pawn.Dead || now - s.tick > GenDate.TicksPerDay * 3);
            EndStrayRegard();
            Changed();
        }

        // ---------------- Faces the colony met ----------------

        /// <summary>
        /// Someone of a people the colony met is going home (set free, tended, a camp's leader, a guest of rank, an envoy, a
        /// visiting trader): they become a face the colony knows, remembering <paramref name="by"/>.
        /// </summary>
        public static void Notify_Met(Pawn p, string met, Pawn by)
        {
            var book = Instance;
            if (!On || book == null || p == null || p.Dead || p.Discarded || p.RaceProps == null || !p.RaceProps.Humanlike || !FactionMemory.Remembers(p.Faction)) return;
            if (p.DevelopmentalStage.Juvenile() || p.IsPrisonerOfColony) return;
            try
            {
                book.Met(p, met, KinMemory.Ours(by) ? by : null);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("notable met", e);
            }
        }

        private void Met(Pawn p, string met, Pawn by)
        {
            var known = notables.FirstOrDefault(n => n.pawn == p);
            float warm = MetWarmth(met);
            if (known != null)
            {
                if (warm > 0f)
                {
                    known.personal += warm * 0.5f;
                    if (by != null) known.RegardOf(by, true).personal += warm;
                    known.Remember(MetNote(met, by), warm * 0.5f);
                }
                return;
            }
            var map = Home;
            var town = map != null ? WorldSituationWorker.NearestTown(p.Faction, map) : null;
            var n = Add(p, town, NotableRole.Acquaintance, false, met, by);
            n.faction = p.Faction;
            n.personal = warm;
            if (by != null) n.RegardOf(by, true).personal = warm * 1.5f;
            n.Remember(MetNote(met, by), warm);
            // The colony knows ten faces at most: the oldest with nothing since make way.
            var faces = notables.Where(x => x.role == NotableRole.Acquaintance).ToList();
            while (faces.Count > NotableMath.MaxAcquaintances)
            {
                var old = faces.Where(x => x != n).OrderBy(x => x.notes.Count == 0 ? 0 : x.notes.Max(z => z.tick)).First();
                faces.Remove(old);
                Drop(old);
            }
        }

        /// <summary>How much better they think of the colony for how they met.</summary>
        private static float MetWarmth(string met)
        {
            switch (met)
            {
                case "freed": return 25f;
                case "tended": return 18f;
                case "guest": return 12f;
                case "camp": return 10f;
                case "envoy": return 4f;
                default: return 3f;
            }
        }

        private static string MetNote(string met, Pawn by)
        {
            string key = "CC_Notable_Note_Met_" + (met ?? "visitor");
            if (!key.CanTranslate()) key = "CC_Notable_Note_Met_visitor";
            return key.Translate((by?.LabelShort ?? "CC_Notable_TheColony".Translate().Resolve()).Named("BY")).Resolve();
        }

        /// <summary>
        /// Someone of a people here at the colony (an envoy, a guest of rank): known by face once they're home, as
        /// <paramref name="met"/>, remembering <paramref name="by"/>.
        /// </summary>
        public static void Notify_Seen(Pawn p, string met, Pawn by = null)
        {
            var book = Instance;
            if (!On || book == null || p == null || p.Dead || p.RaceProps?.Humanlike != true || !FactionMemory.Remembers(p.Faction)) return;
            try
            {
                var s = book.seen.FirstOrDefault(x => x.pawn == p);
                if (s == null)
                {
                    s = new SeenVisitor { pawn = p };
                    book.seen.Add(s);
                    while (book.seen.Count > 6) book.seen.RemoveAt(0);
                }
                s.met = met;
                s.by = KinMemory.Ours(by) ? by : null;
                s.tick = Now;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("notable seen", e);
            }
        }

        /// <summary>Traders and visitors at the colony now: noted, to be known once they're home.</summary>
        private void Seen(int now)
        {
            foreach (var map in Find.Maps.Where(m => m.IsPlayerHome))
            {
                foreach (var lord in map.lordManager.lords)
                {
                    string met = lord.LordJob is LordJob_TradeWithColony ? "trader" : lord.LordJob is LordJob_VisitColony ? "visitor" : null;
                    if (met == null || lord.faction == null || !FactionMemory.Remembers(lord.faction)) continue;
                    // One face a group: their trader, else the one who speaks best for them.
                    var face = met == "trader" ? lord.ownedPawns.FirstOrDefault(p => p.RaceProps.Humanlike && p.TraderKind != null)
                        : lord.ownedPawns.Where(p => p.RaceProps.Humanlike && !p.DevelopmentalStage.Juvenile()).OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0).FirstOrDefault();
                    if (face == null || seen.Any(s => s.pawn == face) || notables.Any(n => n.pawn == face)) continue;
                    seen.Add(new SeenVisitor { pawn = face, met = met, tick = now });
                    while (seen.Count > 6) seen.RemoveAt(0);
                }
            }
            foreach (var s in seen.ToList())
            {
                var p = s.pawn;
                if (p == null || p.Discarded || p.Dead)
                {
                    seen.Remove(s);
                    continue;
                }
                if (p.Spawned || !Find.WorldPawns.Contains(p)) continue;
                seen.Remove(s);
                Notify_Met(p, s.met, s.by);
            }
        }

        // ---------------- How they regard the colony ----------------

        /// <summary>Their nature, -2 (abrasive, cold) to 2 (kind), from their own traits.</summary>
        public static int Warmth(Pawn p)
        {
            var traits = p?.story?.traits?.allTraits;
            if (traits == null) return 0;
            int w = 0;
            foreach (var t in traits)
            {
                switch (t?.def?.defName)
                {
                    case "Kind": w += 2; break;
                    case "Abrasive": w -= 2; break;
                    case "Psychopath": w -= 1; break;
                    case "Greedy":
                    case "Jealous":
                    case "Bloodlust": w -= 1; break;
                }
            }
            return NotableMath.Clamp(w, -2, 2);
        }

        /// <summary>A people at war with the colony.</summary>
        public static bool Hostile(Notable n) => n?.faction != null && n.faction.HostileTo(Faction.OfPlayer);

        /// <summary>How their people stands with the colony, as their own regard starts from.</summary>
        public static int Baseline(Notable n)
        {
            var f = n?.faction;
            if (f == null) return 0;
            int goodwill = f.HasGoodwill ? f.PlayerGoodwill : 0;
            float favor = 0f, wrong = 0f, owed = 0f;
            var mem = FactionMemory.Instance;
            if (mem != null && FactionMemory.On)
            {
                favor = mem.Sum(f, DeedSide.Favor);
                wrong = mem.Sum(f, DeedSide.Wrong);
                owed = mem.Sum(f, DeedSide.TheirFavor, true);
            }
            return NotableMath.Baseline(goodwill, favor, wrong, owed, Warmth(n.pawn), Hostile(n));
        }

        /// <summary>How they regard the colony, -100 to 100.</summary>
        public static int Opinion(Notable n) => n == null ? 0 : NotableMath.Opinion(Baseline(n), n.personal);

        /// <summary>How they regard one colonist, -100 to 100.</summary>
        public static int OpinionOf(Notable n, Pawn p)
        {
            if (n == null || p == null) return 0;
            var deed = KinMemory.DeedBy(n.faction, p);
            return NotableMath.OfColonist(Opinion(n), deed?.Strength ?? 0f, n.RegardOf(p, false)?.personal ?? 0f);
        }

        /// <summary>The colonists they know, best regarded first, with what they know them for.</summary>
        public static List<(Pawn pawn, string why)> Known(Notable n)
        {
            var list = new List<(Pawn, string)>();
            if (n == null) return list;
            var seenP = new HashSet<Pawn>();
            void Add(Pawn p, string why)
            {
                if (!KinMemory.Ours(p) || !seenP.Add(p)) return;
                list.Add((p, why));
            }
            if (n.metBy != null) Add(n.metBy, ("CC_Notable_KnowsFor_" + (n.met ?? "visitor")).CanTranslate() ? ("CC_Notable_KnowsFor_" + n.met).Translate().Resolve() : null);
            foreach (var r in n.regards.Where(r => r.pawn != null).OrderByDescending(r => r.personal)) Add(r.pawn, "CC_Notable_KnowsFor_Letters".Translate().Resolve());
            var friend = KinMemory.Friend(n.faction, out var deed);
            if (friend != null) Add(friend, FactionMemory.Text(deed));
            return list;
        }

        // ---------------- Words ----------------

        /// <summary>"chief", "headwoman", "reeve", "mayor", "administrator", "heir", "envoy", "trader": what they are to their town.</summary>
        public static string TitleOf(Notable n)
        {
            if (n == null) return "";
            int tech = (int)(n.faction?.def?.techLevel ?? TechLevel.Industrial);
            string era = tech <= NotableMath.Neolithic ? "Tribal" : tech == NotableMath.Medieval ? "Medieval" : tech == NotableMath.Industrial ? "Industrial" : "Spacer";
            bool female = n.pawn?.gender == Gender.Female;
            switch (n.role)
            {
                case NotableRole.Head:
                    if (!n.made && n.faction != null && n.pawn != null && n.faction.leader == n.pawn && !n.faction.LeaderTitle.NullOrEmpty()) return n.faction.LeaderTitle;
                    return ("CC_Notable_Title_Head_" + era + (female ? "_F" : "")).CanTranslate()
                        ? ("CC_Notable_Title_Head_" + era + (female ? "_F" : "")).Translate().Resolve()
                        : ("CC_Notable_Title_Head_" + era).Translate().Resolve();
                case NotableRole.Heir: return "CC_Notable_Title_Heir".Translate().Resolve();
                case NotableRole.Envoy: return ("CC_Notable_Title_Envoy_" + era).Translate().Resolve();
                case NotableRole.Trader: return ("CC_Notable_Title_Trader_" + era).Translate().Resolve();
                default: return ("CC_Notable_Title_Met_" + (n.met ?? "visitor")).CanTranslate() ? ("CC_Notable_Title_Met_" + n.met).Translate().Resolve() : "CC_Notable_Title_Met_visitor".Translate().Resolve();
            }
        }

        /// <summary>"Mayor Ana Bell of Kettleford" / "Ana Bell, who was set free by Bo".</summary>
        public static string Called(Notable n)
        {
            if (n == null) return "?";
            if (n.role == NotableRole.Acquaintance) return "CC_Notable_Called_Met".Translate(n.Name.Named("NAME"), TitleOf(n).Named("TITLE"), (n.townName ?? n.faction?.Name ?? "").Named("TOWN")).Resolve();
            return "CC_Notable_Called".Translate(n.Name.Named("NAME"), TitleOf(n).Named("TITLE"), (n.townName ?? "").Named("TOWN")).Resolve();
        }

        /// <summary>"holds the colony dear", "is warm toward the colony", ... "is hostile to the colony".</summary>
        public static string RegardWords(int opinion) => ("CC_Notable_Regard_" + NotableMath.Level(opinion)).Translate().Resolve();

        public static Color RegardColor(int opinion)
        {
            int l = NotableMath.Level(opinion);
            return l <= 1 ? UiKit.Good : l == 2 ? UiKit.Info : l == 3 ? UiKit.Faint : l <= 5 ? UiKit.Warn : UiKit.Bad;
        }

        // ---------------- Saving ----------------

        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                notables.RemoveAll(n => n == null || n.person.IsNone || n.pawn == null && n.name.NullOrEmpty());
                seen.RemoveAll(s => s == null || s.pawn == null);
                letters.RemoveAll(l => l == null || !l.Open && Now - l.stageTick > GenDate.TicksPerDay * 2);
            }
            Scribe_Collections.Look(ref notables, "notables", LookMode.Deep);
            Scribe_Collections.Look(ref letters, "letters", LookMode.Deep);
            Scribe_Collections.Look(ref losses, "losses", LookMode.Deep);
            Scribe_Collections.Look(ref seen, "seen", LookMode.Deep);
            Scribe_Values.Look(ref nextId, "nextId", 1);
            Scribe_Values.Look(ref nextLetterId, "nextLetterId", 1);
            Scribe_Values.Look(ref agedDay, "agedDay", -1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                notables ??= new List<Notable>();
                notables.RemoveAll(n => n == null);
                letters ??= new List<NeighbourLetter>();
                letters.RemoveAll(l => l == null);
                losses ??= new List<NotableLoss>();
                losses.RemoveAll(l => l == null || l.faction == null);
                seen ??= new List<SeenVisitor>();
                seen.RemoveAll(s => s == null || s.pawn == null);
                NoteTowns();
                Changed();
            }
        }
    }
}
