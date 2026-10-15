using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>Whose deed it was, and who it was done to.</summary>
    public enum DeedSide
    {
        /// <summary>The colony did them a good turn: they are grateful (a prisoner set free, a gift, help in a hard year).</summary>
        Favor,
        /// <summary>The colony wronged them: they hold it against the colony (traders robbed, a camp driven off, their dead).</summary>
        Wrong,
        /// <summary>They did the colony a good turn: the colony owes them (fighters sent when it called, relief in a hard time).</summary>
        TheirFavor,
        /// <summary>They wronged the colony (a raid, a colonist killed): the colony's own grievance.</summary>
        TheirWrong
    }

    /// <summary>One thing a people remembers about the colony: what, when, how big, and whether it has been settled since.</summary>
    public class FactionDeed : IExposable
    {
        public int id;
        public Faction faction;
        /// <summary>What it was (a key of <see cref="FactionMemory.Kinds"/>, and of the texts: CC_Memory_Deed_kind).</summary>
        public string kind;
        public DeedSide side;
        /// <summary>How big it was when it happened (it fades from there).</summary>
        public int weight;
        /// <summary>When it happened (the last time, for a deed done more than once in a short while).</summary>
        public int tick;
        public int first;
        /// <summary>How many times in that while (traders, raiders fallen).</summary>
        public int count = 1;
        /// <summary>Who or what it was about: a prisoner's name, a quest, a world event, a town.</summary>
        public string name;
        /// <summary>Repaid, called in or put right since (an envoy came about it): it is still remembered, but asks nothing more.</summary>
        public bool settled;

        public float StrengthAt(int now) => MemoryMath.Strength(weight, now - tick);

        public float Strength => StrengthAt(Find.TickManager.TicksGame);

        public bool Theirs => side == DeedSide.TheirFavor || side == DeedSide.TheirWrong;

        public bool Kind => side == DeedSide.Favor || side == DeedSide.TheirFavor;

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_References.Look(ref faction, "faction");
            Scribe_Values.Look(ref kind, "kind");
            Scribe_Values.Look(ref side, "side");
            Scribe_Values.Look(ref weight, "weight");
            Scribe_Values.Look(ref tick, "tick");
            Scribe_Values.Look(ref first, "first");
            Scribe_Values.Look(ref count, "count", 1);
            Scribe_Values.Look(ref name, "name");
            Scribe_Values.Look(ref settled, "settled");
        }
    }

    /// <summary>A kind of deed: which side it falls on, how big it is, and how close together two of them count as one.</summary>
    public sealed class DeedKind
    {
        public DeedSide side;
        public int weight;
        /// <summary>Another within this many days adds to the first instead of standing alone (0: never).</summary>
        public float mergeDays;
        /// <summary>The most a run of them adds up to.</summary>
        public int max = MemoryMath.MaxWeight;
    }

    /// <summary>
    /// What the world's peoples remember of the colony over the years: debts, favors and grudges, each with its date
    /// and size, fading slowly (the bigger, the slower: see <see cref="MemoryMath"/>), at most a dozen per people. Most
    /// deeds come in through the game's own goodwill reasons (a prisoner released, a wounded guest tended, a gift, a
    /// caravan attacked, peace talks; Harmony hooks in Patch_FactionMemory.cs), and the rest from what happens: raids
    /// and their dead, quests and Rimfolk's missions done or failed, camps helped or driven off, and world events where
    /// the colony stood by a people. Rimfolk events and missions follow from it (an old debt repaid or called in, relief
    /// in a hard time, an old enemy offering peace, an invitation, a quarrel to settle), raid letters name the old wrong,
    /// and the World page shows what each people remembers. Its own game component, saved with the game.
    /// </summary>
    public class FactionMemory : GameComponent
    {
        public List<FactionDeed> deeds = new List<FactionDeed>();
        public int nextId = 1;
        /// <summary>World events that ended up to this tick have been read for the peoples the colony helped.</summary>
        public int situationsRead = -1;

        private static FactionMemory cachedInstance;
        private static Game cachedGame;

        /// <summary>Bumped on every change (the World page rebuilds its section when it moves).</summary>
        public static int Version { get; private set; }

        public FactionMemory(Game game)
        {
        }

        public static FactionMemory Instance
        {
            get
            {
                var game = Current.Game;
                if (game == null) return null;
                if (cachedGame != game || cachedInstance == null)
                {
                    cachedInstance = game.GetComponent<FactionMemory>();
                    cachedGame = game;
                }
                return cachedInstance;
            }
        }

        /// <summary>Switched on, and a game to remember in.</summary>
        public static bool On => (ColonyCouncilMod.Settings?.factionMemory ?? true) && Instance != null;

        private static int Now => Find.TickManager.TicksGame;

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            cachedInstance = this;
            cachedGame = Current.Game;
            Version++;
        }

        // ---------------- The kinds of deed ----------------

        private static DeedKind K(DeedSide side, int weight, float mergeDays = 0f, int max = MemoryMath.MaxWeight) =>
            new DeedKind { side = side, weight = weight, mergeDays = mergeDays, max = max };

        /// <summary>Every kind of deed, with its side and size. The texts are CC_Memory_Deed_kind (and _Name, _Many).</summary>
        public static readonly Dictionary<string, DeedKind> Kinds = new Dictionary<string, DeedKind>
        {
            // The colony's good turns.
            ["released"] = K(DeedSide.Favor, 12, 3f, 30),
            ["tended"] = K(DeedSide.Favor, 10, 3f, 25),
            ["gift"] = K(DeedSide.Favor, 8, 10f, 30),
            ["traded"] = K(DeedSide.Favor, 1, 20f, 8),
            ["quest"] = K(DeedSide.Favor, 10),
            ["mission"] = K(DeedSide.Favor, 10),
            ["campHelped"] = K(DeedSide.Favor, 8),
            ["campHosted"] = K(DeedSide.Favor, 4),
            ["hardship"] = K(DeedSide.Favor, 15),
            ["peaceTalks"] = K(DeedSide.Favor, 8),
            ["feast"] = K(DeedSide.Favor, 8),
            ["dignitary"] = K(DeedSide.Favor, 12),
            ["refugees"] = K(DeedSide.Favor, 20),
            ["sidedWith"] = K(DeedSide.Favor, 10),
            ["mediated"] = K(DeedSide.Favor, 8),
            ["madePeace"] = K(DeedSide.Favor, 8),
            ["welcomed"] = K(DeedSide.Favor, 5),
            ["favorDone"] = K(DeedSide.Favor, 6),
            ["forgave"] = K(DeedSide.Favor, 10),
            // The colony's wrongs.
            ["robbed"] = K(DeedSide.Wrong, 20, 2f),
            ["attacked"] = K(DeedSide.Wrong, 6, 2f, 30),
            ["caravanRaided"] = K(DeedSide.Wrong, 20, 2f),
            ["killed"] = K(DeedSide.Wrong, 15, 2f),
            ["captured"] = K(DeedSide.Wrong, 8, 2f, 25),
            ["sold"] = K(DeedSide.Wrong, 15, 5f),
            ["prisonerDied"] = K(DeedSide.Wrong, 12, 3f, 30),
            ["fell"] = K(DeedSide.Wrong, 3, 2f, 30),
            ["slain"] = K(DeedSide.Wrong, 4, 2f, 30),
            ["townAttacked"] = K(DeedSide.Wrong, 25, 3f),
            ["townDestroyed"] = K(DeedSide.Wrong, 45),
            ["polluted"] = K(DeedSide.Wrong, 6, 15f, 20),
            ["questFailed"] = K(DeedSide.Wrong, 8),
            ["missionFailed"] = K(DeedSide.Wrong, 6),
            ["campDriven"] = K(DeedSide.Wrong, 10),
            ["campHarmed"] = K(DeedSide.Wrong, 15),
            ["campSour"] = K(DeedSide.Wrong, 5),
            ["peaceTalksBad"] = K(DeedSide.Wrong, 6),
            ["slighted"] = K(DeedSide.Wrong, 8),
            ["letDown"] = K(DeedSide.Wrong, 8),
            ["absent"] = K(DeedSide.Wrong, 4),
            ["unfed"] = K(DeedSide.Wrong, 8),
            ["tookSides"] = K(DeedSide.Wrong, 10),
            ["refused"] = K(DeedSide.Wrong, 6),
            // Their good turns: the colony owes them.
            ["aid"] = K(DeedSide.TheirFavor, 12, 2f),
            ["relief"] = K(DeedSide.TheirFavor, 10),
            ["taught"] = K(DeedSide.TheirFavor, 8),
            // Their wrongs.
            ["raided"] = K(DeedSide.TheirWrong, 6, 1f, 20),
            ["killedOurs"] = K(DeedSide.TheirWrong, 15, 2f)
        };

        public static DeedKind KindOf(string kind) => kind != null && Kinds.TryGetValue(kind, out var k) ? k : null;

        /// <summary>A people that remembers: one of the world's real peoples, not the colony, nor a hidden or passing one.</summary>
        public static bool Remembers(Faction f) =>
            f != null && !f.IsPlayer && !f.Hidden && !f.temporary && f.def != null && f.def.humanlikeFaction;

        // ---------------- Remembering ----------------

        /// <summary>
        /// A deed is remembered (if the colony's memory is on and the people one that remembers). The same kind again soon
        /// after adds to the first. <paramref name="weight"/> 0: the kind's own size. Returns the deed, or null.
        /// </summary>
        public static FactionDeed Note(Faction f, string kind, string name = null, int weight = 0, int count = 1)
        {
            if (!On || !Remembers(f) || f.defeated) return null;
            var k = KindOf(kind);
            if (k == null) return null;
            try
            {
                return Instance.Add(f, kind, k, name, weight > 0 ? weight : k.weight, Mathf.Max(1, count));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("faction memory " + kind, e);
                return null;
            }
        }

        private FactionDeed Add(Faction f, string kind, DeedKind k, string name, int weight, int count)
        {
            int now = Now;
            weight = Mathf.Clamp(weight, 1, Mathf.Min(k.max, MemoryMath.MaxWeight));
            if (k.mergeDays > 0f)
            {
                var old = deeds.FirstOrDefault(d => d.faction == f && d.kind == kind && !d.settled && now - d.tick <= k.mergeDays * GenDate.TicksPerDay);
                if (old != null)
                {
                    old.weight = MemoryMath.Merged(old.weight, now - old.tick, weight, k.max);
                    old.count += count;
                    old.tick = now;
                    if (old.name.NullOrEmpty()) old.name = name;
                    Version++;
                    return old;
                }
            }
            var d = new FactionDeed { id = nextId++, faction = f, kind = kind, side = k.side, weight = weight, tick = now, first = now, count = count, name = name };
            deeds.Add(d);
            Trim(f, now);
            Version++;
            return d;
        }

        /// <summary>A people keeps at most <see cref="MemoryMath.PerFaction"/> deeds: past that, the settled and the faint make way.</summary>
        private void Trim(Faction f, int now)
        {
            var mine = deeds.Where(d => d.faction == f).ToList();
            while (mine.Count > MemoryMath.PerFaction)
            {
                int i = MemoryMath.Weakest(mine.Select(d => d.StrengthAt(now)).ToList(), mine.Select(d => d.settled).ToList());
                if (i < 0) break;
                deeds.Remove(mine[i]);
                mine.RemoveAt(i);
            }
        }

        /// <summary>Repaid, called in or put right: it asks nothing more of anyone.</summary>
        public static void Settle(FactionDeed d)
        {
            if (d == null || d.settled) return;
            d.settled = true;
            Version++;
        }

        public FactionDeed ById(int id) => deeds.FirstOrDefault(d => d.id == id);

        // ---------------- Once a day ----------------

        /// <summary>The faded are forgotten, the gone peoples with them, and world events that ended are read for who was helped.</summary>
        public static void Daily(int now)
        {
            var mem = Instance;
            if (mem == null) return;
            int before = mem.deeds.Count;
            mem.deeds.RemoveAll(d => d == null || d.faction == null || d.faction.defeated || d.kind == null || d.StrengthAt(now) < MemoryMath.Forgotten);
            if (mem.deeds.Count != before) Version++;
            if (On) mem.ReadSituations();
        }

        /// <summary>A world event that ended with the colony having stood by its people: they remember it (older saves are read once).</summary>
        private void ReadSituations()
        {
            var history = WorldSituations.Instance?.history;
            if (history == null) return;
            int latest = situationsRead;
            foreach (var r in history)
            {
                if (r == null || r.endTick <= situationsRead) continue;
                latest = Mathf.Max(latest, r.endTick);
                if (!r.helped || !Remembers(r.faction) || r.faction.defeated) continue;
                var k = KindOf("hardship");
                string what = DefDatabase<WorldSituationDef>.GetNamedSilentFail(r.defName ?? "")?.label ?? r.label;
                var d = Add(r.faction, "hardship", k, what, k.weight + 3 * Mathf.Clamp(r.help, 0, 6), 1);
                // It happened when the world event ended, not today.
                d.tick = d.first = Mathf.Min(Now, r.endTick);
            }
            situationsRead = latest;
        }

        // ---------------- What a people remembers ----------------

        public IEnumerable<FactionDeed> Of(Faction f) => deeds.Where(d => d.faction == f);

        /// <summary>How much this side of the ledger adds up to now (only what is still owed, with <paramref name="openOnly"/>).</summary>
        public float Sum(Faction f, DeedSide side, bool openOnly = false)
        {
            int now = Now;
            float sum = 0f;
            foreach (var d in deeds)
                if (d.faction == f && d.side == side && (!openOnly || !d.settled))
                    sum += d.StrengthAt(now);
            return sum;
        }

        /// <summary>The biggest deed on this side still remembered at least so strongly, at least so many days old (unsettled ones only, with <paramref name="open"/>).</summary>
        public FactionDeed Best(Faction f, DeedSide side, float minStrength = 1f, float minAgeDays = 0f, bool open = true, Func<FactionDeed, bool> pred = null)
        {
            int now = Now;
            FactionDeed best = null;
            float bestS = minStrength;
            foreach (var d in deeds)
            {
                if (d.faction != f || d.side != side || open && d.settled || now - d.tick < minAgeDays * GenDate.TicksPerDay) continue;
                if (pred != null && !pred(d)) continue;
                float s = d.StrengthAt(now);
                if (s >= bestS)
                {
                    best = d;
                    bestS = s;
                }
            }
            return best;
        }

        /// <summary>The newest deed of either side between the colony and a people (its age in ticks), or int.MaxValue for none.</summary>
        public int SinceLast(Faction f, params DeedSide[] sides)
        {
            int now = Now;
            int best = int.MaxValue;
            foreach (var d in deeds)
                if (d.faction == f && (sides.Length == 0 || sides.Contains(d.side)))
                    best = Mathf.Min(best, now - d.tick);
            return best;
        }

        /// <summary>The peoples that remember something, the most strongly first.</summary>
        public List<Faction> Peoples()
        {
            int now = Now;
            return deeds.Where(d => d.faction != null && !d.faction.defeated).GroupBy(d => d.faction)
                .OrderByDescending(g => g.Sum(d => d.StrengthAt(now))).Select(g => g.Key).ToList();
        }

        /// <summary>How the people stands with the colony, from its memory (see <see cref="MemoryMath.Tone"/>).</summary>
        public int Tone(Faction f) => MemoryMath.Tone(Sum(f, DeedSide.Favor), Sum(f, DeedSide.Wrong), Sum(f, DeedSide.TheirFavor, true));

        // ---------------- Texts ----------------

        /// <summary>
        /// The deed in a few words, without who did it ("set Ana free", "sent fighters when the colony called"): for the
        /// World page under who did it, and for {TALE} in the letters that follow from it.
        /// </summary>
        public static string Text(FactionDeed d)
        {
            if (d == null) return "";
            string key = "CC_Memory_Deed_" + d.kind;
            string named = d.name.NullOrEmpty() ? null : d.name;
            try
            {
                if (d.count > 1 && (key + "_Many").CanTranslate())
                    return (key + "_Many").Translate(d.count.Named("COUNT"), (named ?? "").Named("NAME"), (d.faction?.Name ?? "").Named("FACTION")).Resolve();
                if (named != null && (key + "_Name").CanTranslate())
                    return (key + "_Name").Translate(named.Named("NAME"), d.count.Named("COUNT"), (d.faction?.Name ?? "").Named("FACTION")).Resolve();
                if (key.CanTranslate())
                    return key.Translate(d.count.Named("COUNT"), (named ?? "").Named("NAME"), (d.faction?.Name ?? "").Named("FACTION")).Resolve();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("faction memory text " + d.kind, e);
            }
            return d.kind;
        }

        /// <summary>"Lately", "40 days ago", "last year", "3 years ago".</summary>
        public static string AgoText(int tick)
        {
            switch (MemoryMath.AgoOf(Now - tick, out int n))
            {
                case MemoryMath.Ago.Lately: return "CC_Memory_Ago_Lately".Translate();
                case MemoryMath.Ago.Days: return "CC_Memory_Ago_Days".Translate(n);
                case MemoryMath.Ago.LastYear: return "CC_Memory_Ago_LastYear".Translate();
                default: return "CC_Memory_Ago_Years".Translate(n);
            }
        }

        /// <summary>
        /// For the game's raid letter: the old wrong these raiders have come about, or how often they've come before.
        /// Null when the colony's memory is off or there's nothing true to say.
        /// </summary>
        public static string RaidNote(Faction f)
        {
            var mem = Instance;
            if (!On || mem == null || !Remembers(f)) return null;
            try
            {
                var wrong = mem.Best(f, DeedSide.Wrong, 6f, 1f, false);
                if (wrong != null)
                {
                    string key = "CC_Memory_Raid_" + wrong.kind;
                    if (!key.CanTranslate()) key = "CC_Memory_Raid";
                    return key.Translate(f.Name.Named("FACTION"), Text(wrong).Named("DEED"), AgoText(wrong.tick).Named("WHEN"), (wrong.name ?? "").Named("NAME")).Resolve();
                }
                int raids = mem.deeds.Where(d => d.faction == f && d.kind == "raided").Sum(d => d.count);
                if (raids >= 3) return "CC_Memory_Raid_Again".Translate(f.Name.Named("FACTION"), raids.Named("COUNT")).Resolve();
                var killed = mem.Best(f, DeedSide.TheirWrong, 6f, 1f, false, d => d.kind == "killedOurs" && !d.name.NullOrEmpty());
                if (killed != null) return "CC_Memory_Raid_TheyKilled".Translate(f.Name.Named("FACTION"), killed.name.Named("NAME"), AgoText(killed.tick).Named("WHEN")).Resolve();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("faction memory raid note", e);
            }
            return null;
        }

        // ---------------- Saving ----------------

        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode == LoadSaveMode.Saving) deeds.RemoveAll(d => d == null || d.faction == null);
            Scribe_Collections.Look(ref deeds, "deeds", LookMode.Deep);
            Scribe_Values.Look(ref nextId, "nextId", 1);
            Scribe_Values.Look(ref situationsRead, "situationsRead", -1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (deeds == null) deeds = new List<FactionDeed>();
                deeds.RemoveAll(d => d == null || d.faction == null || d.kind == null);
                Version++;
            }
        }
    }
}
