using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Goods held in trust for a people (docs/peoples-events-proposals.md, item 1, and later item 20): what was left in the
    /// colony's care, by whom, until when, and how each crate went (opened, sold, burned, gone), noted as it happened
    /// (TrustCrate). Its fetch is one of the events' visits (PeopleTrust.VisitKind).
    /// </summary>
    public class TrustRecord : IExposable
    {
        public int id;
        public Faction faction;
        public Map map;
        /// <summary>The crates left, while they stand (a crate destroyed is gone from here on a load, its fate noted below).</summary>
        public List<Thing> crates = new List<Thing>();
        /// <summary>Every crate's id, the day they came.</summary>
        public List<int> ids = new List<int>();
        /// <summary>How each crate went that isn't simply there: "id:fate" (PeopleEventsMath.Opened, Sold, Burned, Gone).</summary>
        public List<string> fates = new List<string>();
        public int left = -1, due = -1;
        /// <summary>The fee at the fetch (0: kept for nothing, as friends).</summary>
        public int fee;
        /// <summary>The caravan master who wrote the colony into the book, by name.</summary>
        public string master;
        /// <summary>The league's means: "beast", "truck", "lander" (PeopleEventsMath.Means).</summary>
        public string means;
        /// <summary>Fetched (or lapsed): nothing more to watch.</summary>
        public bool done;
        /// <summary>What the book said: "good", "red", "lapsed".</summary>
        public string ink;

        public string FateOf(int crate)
        {
            string pre = crate + ":";
            var f = fates.FirstOrDefault(x => x.StartsWith(pre));
            return f?.Substring(pre.Length);
        }

        public void Note(int crate, string fate)
        {
            if (!ids.Contains(crate) || FateOf(crate) != null) return;
            fates.Add(crate + ":" + fate);
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_References.Look(ref faction, "faction");
            Scribe_References.Look(ref map, "map");
            Scribe_Collections.Look(ref crates, "crates", LookMode.Reference);
            Scribe_Collections.Look(ref ids, "ids", LookMode.Value);
            Scribe_Collections.Look(ref fates, "fates", LookMode.Value);
            Scribe_Values.Look(ref left, "left", -1);
            Scribe_Values.Look(ref due, "due", -1);
            Scribe_Values.Look(ref fee, "fee");
            Scribe_Values.Look(ref master, "master");
            Scribe_Values.Look(ref means, "means");
            Scribe_Values.Look(ref done, "done");
            Scribe_Values.Look(ref ink, "ink");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                crates ??= new List<Thing>();
                crates.RemoveAll(t => t == null);
                ids ??= new List<int>();
                fates ??= new List<string>();
            }
        }
    }

    /// <summary>
    /// What the peoples' own events keep with the game (Peoples/Events/): the colonists who have had a people's gift (one
    /// of each kind a colonist; PeopleEventsMath.MaxGifts at most) and when the last came, the goods held in trust, the
    /// Mossborn's promise and its reckonings, the old places the scholars have read. A save from before has none of it.
    /// </summary>
    public class PeopleEventsBook : GameComponent
    {
        /// <summary>"kind:id" for each colonist given a people's gift of that kind (PeopleEventsMath.GiftKey).</summary>
        public List<string> gifts = new List<string>();
        public int lastGift = -1;
        public List<TrustRecord> trusts = new List<TrustRecord>();
        public int nextTrust = 1;
        /// <summary>Until when the league's good ink holds (better terms with every league) and its red ink (no league caravans); -1 none.</summary>
        public int goodInkUntil = -1, redInkUntil = -1;
        /// <summary>The Mossborn's promise: when it was made (-1: none), and how the reckonings since went.</summary>
        public int promiseSince = -1, promisesKept, promisesBroken;
        /// <summary>The old places the scholars have measured ("map:x,z" or "map:land"), so none is read twice.</summary>
        public List<string> read = new List<string>();

        private static PeopleEventsBook cached;
        private static Game cachedGame;

        public PeopleEventsBook(Game game)
        {
        }

        public static PeopleEventsBook Instance
        {
            get
            {
                var game = Current.Game;
                if (game == null) return null;
                if (cachedGame != game || cached == null)
                {
                    cached = game.GetComponent<PeopleEventsBook>();
                    cachedGame = game;
                }
                return cached;
            }
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            cached = this;
            cachedGame = Current.Game;
        }

        // ================= Gifts =================

        public static bool Given(string kind, Pawn p) => p != null && Instance != null && Instance.gifts.Contains(PeopleEventsMath.GiftKey(kind, p.thingIDNumber));

        /// <summary>A people's gift may come now (the gap since the last one).</summary>
        public static bool GiftOpen => Instance == null || PeopleEventsMath.GiftOpen(Find.TickManager.TicksGame, Instance.lastGift);

        public static void Gave(string kind, Pawn p)
        {
            var b = Instance;
            if (b == null || p == null) return;
            string key = PeopleEventsMath.GiftKey(kind, p.thingIDNumber);
            if (!b.gifts.Contains(key)) b.gifts.Add(key);
            while (b.gifts.Count > PeopleEventsMath.MaxGifts) b.gifts.RemoveAt(0);
            b.lastGift = Find.TickManager.TicksGame;
        }

        // ================= Old places read =================

        public static bool Read(string key) => key != null && Instance != null && Instance.read.Contains(key);

        public static void NoteRead(string key)
        {
            var b = Instance;
            if (b == null || key == null || b.read.Contains(key)) return;
            b.read.Add(key);
            while (b.read.Count > PeopleEventsMath.MaxRead) b.read.RemoveAt(0);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref gifts, "gifts", LookMode.Value);
            Scribe_Values.Look(ref lastGift, "lastGift", -1);
            Scribe_Collections.Look(ref trusts, "trusts", LookMode.Deep);
            Scribe_Values.Look(ref nextTrust, "nextTrust", 1);
            Scribe_Values.Look(ref goodInkUntil, "goodInkUntil", -1);
            Scribe_Values.Look(ref redInkUntil, "redInkUntil", -1);
            Scribe_Values.Look(ref promiseSince, "promiseSince", -1);
            Scribe_Values.Look(ref promisesKept, "promisesKept");
            Scribe_Values.Look(ref promisesBroken, "promisesBroken");
            Scribe_Collections.Look(ref read, "read", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                gifts ??= new List<string>();
                trusts ??= new List<TrustRecord>();
                trusts.RemoveAll(t => t == null);
                read ??= new List<string>();
            }
        }
    }
}
