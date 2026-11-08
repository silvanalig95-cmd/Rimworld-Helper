using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// One dealing with a whole people or one of their towns (Dealings/Dealings.cs), or the colony's answer to a letter the
    /// world wrote first: what it is, with whom, the letter it went by, where it stands, its terms and what has come of it.
    /// </summary>
    public class Dealing : IExposable
    {
        public int id;
        public DealKind kind;
        public byte sub;
        public Faction people;
        /// <summary>The town it is with (the settlement's ID), or -1 for the people as a whole; its name kept for when it's gone.</summary>
        public int town = -1;
        public string townName;
        public Map home;
        /// <summary>The letter it went by (World/NeighbourLetters.cs), and the notable who received it for their council.</summary>
        public int letter = -1;
        public int notable = -1;
        public DealStage stage;
        public int asked = -1, since = -1, until = -1;
        /// <summary>When the next thing happens: a delivery, the fair's morning, the work done, their caravan due.</summary>
        public int next = -1;
        /// <summary>The terms: goods (a defName, or PlayCarry's word: "medicine", "meals", "rawfood", "wood", "blocks"), how many, the silver.</summary>
        public string goods;
        public int count, silver;
        /// <summary>Deliveries kept and missed (in all, and in a row); a work's share collected (1).</summary>
        public int done, missed, missedRow;
        /// <summary>The courier trip under way for it (their porters at the gate), the camp it brought, or -1.</summary>
        public int tripId = -1, campId = -1;
        /// <summary>The world letter it answers (a Reply), or -1.</summary>
        public int askId = -1;
        /// <summary>Their answer once read (agreed); for a Reply, the colony's own (granted).</summary>
        public bool yes;
        /// <summary>Their caravan came for the fair or the terms posted; their people came for the feast.</summary>
        public bool came;

        public bool Live => stage == DealStage.Asked || stage == DealStage.Agreed || stage == DealStage.Running;

        public Settlement Town => NotableBook.TownOf(town);

        public void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving && home != null && !Find.Maps.Contains(home)) home = null;
            Scribe_Values.Look(ref id, "id");
            Scribe_Values.Look(ref kind, "kind");
            Scribe_Values.Look(ref sub, "sub");
            Scribe_References.Look(ref people, "people");
            Scribe_Values.Look(ref town, "town", -1);
            Scribe_Values.Look(ref townName, "townName");
            Scribe_References.Look(ref home, "home");
            Scribe_Values.Look(ref letter, "letter", -1);
            Scribe_Values.Look(ref notable, "notable", -1);
            Scribe_Values.Look(ref stage, "stage");
            Scribe_Values.Look(ref asked, "asked", -1);
            Scribe_Values.Look(ref since, "since", -1);
            Scribe_Values.Look(ref until, "until", -1);
            Scribe_Values.Look(ref next, "next", -1);
            Scribe_Values.Look(ref goods, "goods");
            Scribe_Values.Look(ref count, "count");
            Scribe_Values.Look(ref silver, "silver");
            Scribe_Values.Look(ref done, "done");
            Scribe_Values.Look(ref missed, "missed");
            Scribe_Values.Look(ref missedRow, "missedRow");
            Scribe_Values.Look(ref tripId, "tripId", -1);
            Scribe_Values.Look(ref campId, "campId", -1);
            Scribe_Values.Look(ref askId, "askId", -1);
            Scribe_Values.Look(ref yes, "yes");
            Scribe_Values.Look(ref came, "came");
        }
    }

    /// <summary>A steady exchange of letters with one notable (Dealings/PenFriends.cs): when the letters passed, and since when they're a pen friend.</summary>
    public class PenFriend : IExposable
    {
        public int notable;
        public List<int> letters = new List<int>();
        /// <summary>A pen friend since this tick (-1: an exchange, not yet a friendship).</summary>
        public int since = -1;
        /// <summary>When their favor was last offered, and their last warning of a raid (-1: never).</summary>
        public int favourAt = -1, warnedAt = -1;

        public bool Friend => since >= 0;

        public void ExposeData()
        {
            Scribe_Values.Look(ref notable, "notable");
            Scribe_Collections.Look(ref letters, "letters", LookMode.Value);
            Scribe_Values.Look(ref since, "since", -1);
            Scribe_Values.Look(ref favourAt, "favourAt", -1);
            Scribe_Values.Look(ref warnedAt, "warnedAt", -1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit) letters ??= new List<int>();
        }
    }

    /// <summary>A letter the world wrote to the colony first that wants an answer (Dealings/WorldWrites.cs).</summary>
    public class WorldAsk : IExposable
    {
        public int id;
        public int notable;
        public Faction people;
        public int town = -1;
        public ReplyKind kind;
        /// <summary>What a request asks for, and how many.</summary>
        public string goods;
        public int count;
        public int written;
        /// <summary>When it opened at the colony (-1: still on its way), and its place in the post while it is.</summary>
        public int opened = -1;
        public int postId = -1;
        public bool answered;
        public Map home;

        public void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving && home != null && !Find.Maps.Contains(home)) home = null;
            Scribe_Values.Look(ref id, "id");
            Scribe_Values.Look(ref notable, "notable");
            Scribe_References.Look(ref people, "people");
            Scribe_Values.Look(ref town, "town", -1);
            Scribe_Values.Look(ref kind, "kind");
            Scribe_Values.Look(ref goods, "goods");
            Scribe_Values.Look(ref count, "count");
            Scribe_Values.Look(ref written, "written");
            Scribe_Values.Look(ref opened, "opened", -1);
            Scribe_Values.Look(ref postId, "postId", -1);
            Scribe_Values.Look(ref answered, "answered");
            Scribe_References.Look(ref home, "home");
        }
    }

    /// <summary>
    /// The dealings' book (docs/design/diplomacy-screen.md, step B; post-and-letters.md, points 12 and 13): the colony's
    /// dealings with whole peoples and towns, its pen friends, and the world's letters waiting for an answer. A game
    /// component of its own, so an older save starts it empty. Looked at on the neighbours' hour (World/Notables.cs).
    /// </summary>
    public class DealingBook : GameComponent
    {
        public List<Dealing> deals = new List<Dealing>();
        public List<PenFriend> friends = new List<PenFriend>();
        public List<WorldAsk> asks = new List<WorldAsk>();
        public int nextId = 1;
        /// <summary>When the world last wrote first (-1: never).</summary>
        public int lastWrote = -1;

        /// <summary>Closed dealings are kept this long for the Dealings tab and the history, then let go.</summary>
        public const float KeepClosedDays = 60f;

        /// <summary>Dealings kept at most (closed ones go first).</summary>
        public const int MaxDeals = 80;

        private static DealingBook cached;
        private static Game cachedGame;

        public DealingBook(Game game)
        {
        }

        public static DealingBook Instance
        {
            get
            {
                var game = Current.Game;
                if (game == null) return null;
                if (cachedGame != game || cached == null)
                {
                    cached = game.GetComponent<DealingBook>();
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
            Dealings.Changed();
        }

        public Dealing Get(int id) => deals.FirstOrDefault(d => d.id == id);

        public Dealing ByLetter(int letterId) => letterId < 0 ? null : deals.FirstOrDefault(d => d.letter == letterId);

        public PenFriend FriendOf(int notable, bool make)
        {
            var f = friends.FirstOrDefault(x => x.notable == notable);
            if (f == null && make)
            {
                f = new PenFriend { notable = notable };
                friends.Add(f);
            }
            return f;
        }

        public WorldAsk Ask(int id) => asks.FirstOrDefault(a => a.id == id);

        /// <summary>A joint work built: kept while its town stands (a watchtower warns, a work is built once with each town).</summary>
        public static bool Standing(Dealing d) => d != null && d.kind == DealKind.Work && d.stage == DealStage.Kept && d.Town != null;

        /// <summary>Closed dealings let go after a while (a work built kept while it stands), and the oldest closed past the cap.</summary>
        public void Tidy(int now)
        {
            int keep = (int)(KeepClosedDays * DealingMath.DayTicks);
            deals.RemoveAll(d => d == null || !d.Live && !Standing(d) && now - (d.until >= 0 ? d.until : d.since >= 0 ? d.since : d.asked) > keep);
            while (deals.Count > MaxDeals)
            {
                var old = deals.Where(d => !d.Live && !Standing(d)).OrderBy(d => d.asked).FirstOrDefault();
                if (old == null) break;
                deals.Remove(old);
            }
            asks.RemoveAll(a => a == null || a.answered && now - a.written > DealingMath.AnswerDays * 2f * DealingMath.DayTicks
                                || now - a.written > (DealingMath.AnswerDays + 30f) * DealingMath.DayTicks);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref deals, "deals", LookMode.Deep);
            Scribe_Collections.Look(ref friends, "friends", LookMode.Deep);
            Scribe_Collections.Look(ref asks, "asks", LookMode.Deep);
            Scribe_Values.Look(ref nextId, "nextId", 1);
            Scribe_Values.Look(ref lastWrote, "lastWrote", -1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                deals ??= new List<Dealing>();
                deals.RemoveAll(d => d == null);
                friends ??= new List<PenFriend>();
                friends.RemoveAll(f => f == null);
                asks ??= new List<WorldAsk>();
                asks.RemoveAll(a => a == null);
            }
        }
    }
}
