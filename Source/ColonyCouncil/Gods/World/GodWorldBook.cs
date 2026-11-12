using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A god's lasting gift to one colonist (Gods/World/GodWorldGifts.cs): which god, to whom, what it is (its kind and the def it
    /// came as: a hediff, a trait, a spell scroll, a path, a beast, gold or a relic), the price paid, and whether a rival has
    /// had it back.
    /// </summary>
    public class GodGiftHeld : IExposable
    {
        public int id;
        public int god = -1;
        public Pawn pawn;
        public GodGiftKind kind;
        /// <summary>The def it came as (Rimfolk_GodGift_Storm, Druid, SpellOf_Heal, VPE_Staticlord, Wolf_Timber, a relic's def).</summary>
        public string what;
        /// <summary>It in words, as the morning's card named it ("a green hand", "the craft of a druid").</summary>
        public string label;
        public GodPrice price;
        public int since;
        /// <summary>A rival came for it: undone (taken back), or it was tried (once only).</summary>
        public bool undone, undoTried;
        /// <summary>The beast or the relic given, while it is.</summary>
        public Thing thing;

        public bool Holds => !undone && pawn != null && !pawn.Dead && !pawn.Discarded;

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_Values.Look(ref god, "god", -1);
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Values.Look(ref kind, "kind", GodGiftKind.None);
            Scribe_Values.Look(ref what, "what");
            Scribe_Values.Look(ref label, "label");
            Scribe_Values.Look(ref price, "price", GodPrice.Offering);
            Scribe_Values.Look(ref since, "since");
            Scribe_Values.Look(ref undone, "undone");
            Scribe_Values.Look(ref undoTried, "undoTried");
            Scribe_References.Look(ref thing, "thing");
        }
    }

    /// <summary>
    /// Something of the gods under way in the world, waiting for its time (Gods/World/): a dream told and waiting for the
    /// colony's word ("offer"), a night under the sky ("receive"), a grudge a god holds against a colonist ("grudge"), the
    /// Hunger come to take ("take"), a temple's envoy ("relic"), one hunted at the gate ("hide") and hidden ("hidden"), two
    /// gods' quarrel in the weather ("quarrel"), a rival's dream asking for a gift back ("undo"), a place one people calls
    /// holy ("site"), the colony's letter that slighted a god ("slight").
    /// One record for all of them: each kind reads the fields it needs.
    /// </summary>
    public class GodDoing : IExposable
    {
        public int id;
        public string kind;
        public int god = -1, other = -1;
        public int home = HomesMath.None;
        public Pawn pawn, who;
        public Faction faction;
        public Thing thing;
        public string what;
        public int count;
        public int at, until = -1;
        public int stage;
        public IntVec3 cell = IntVec3.Invalid;
        public bool done;

        public Map Map => Homes.MapOf(home);

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_Values.Look(ref kind, "kind");
            Scribe_Values.Look(ref god, "god", -1);
            Scribe_Values.Look(ref other, "other", -1);
            Scribe_Values.Look(ref home, "home", HomesMath.None);
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_References.Look(ref who, "who");
            Scribe_References.Look(ref faction, "faction");
            Scribe_References.Look(ref thing, "thing");
            Scribe_Values.Look(ref what, "what");
            Scribe_Values.Look(ref count, "count");
            Scribe_Values.Look(ref at, "at");
            Scribe_Values.Look(ref until, "until", -1);
            Scribe_Values.Look(ref stage, "stage");
            Scribe_Values.Look(ref cell, "cell", IntVec3.Invalid);
            Scribe_Values.Look(ref done, "done");
        }
    }

    /// <summary>
    /// What the gods in the wider world keep with the game (Gods/World/), all of it hidden: the gifts colonists hold, what is
    /// under way, the illnesses a god has already eased (once each), when each kind of thing last came, and the Hunger's
    /// account with the colony. A save from before has none of it.
    /// </summary>
    public class GodWorldBook : GameComponent
    {
        public List<GodGiftHeld> gifts = new List<GodGiftHeld>();
        public List<GodDoing> doings = new List<GodDoing>();
        /// <summary>The illnesses a god has eased (the hediff's load id), so none is eased twice.</summary>
        public List<int> blessed = new List<int>();
        /// <summary>When each kind of thing last came ("invite", "envoy", "gift", "quarrel", "bargain"...), by its key.</summary>
        public Dictionary<string, int> lasts = new Dictionary<string, int>();
        /// <summary>The Hunger's account: bargains struck and what it has taken since.</summary>
        public int hungerBargains, hungerTaken;
        /// <summary>The day's kindness and cruelty, for Selene to return at night (then reset).</summary>
        public int kindness, cruelty;
        public int nextId = 1;

        /// <summary>Gifts kept (the colony's own and the newest let go), things under way, illnesses eased.</summary>
        public const int MaxGifts = 60, MaxDoings = 40, MaxBlessed = 40;

        private static GodWorldBook cached;
        private static Game cachedGame;

        public GodWorldBook(Game game)
        {
        }

        public static GodWorldBook Instance
        {
            get
            {
                var game = Current.Game;
                if (game == null) return null;
                if (cachedGame != game || cached == null)
                {
                    cached = game.GetComponent<GodWorldBook>();
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

        private static int Now => Find.TickManager?.TicksGame ?? 0;

        // ================= When things last came =================

        public int Last(string key) => key != null && lasts.TryGetValue(key, out int t) ? t : -1;

        public void Came(string key) => lasts[key] = Now;

        public bool Gap(string key, float days) => GodWorld.Force || GodWorldMath.Gap(Now, Last(key), days);

        /// <summary>Days since a thing last came (a long time for never), for a chance that grows after its gap (GodPaceMath.Ramp).</summary>
        public float DaysSince(string key)
        {
            int last = Last(key);
            return last < 0 ? 9999f : (Now - last) / (float)GodWorldMath.DayTicks;
        }

        // ================= Under way =================

        public GodDoing Add(GodDoing d)
        {
            if (d == null) return null;
            d.id = nextId++;
            if (d.at <= 0) d.at = Now;
            doings.Add(d);
            // Too many under way: the oldest that has an end lets go first.
            while (doings.Count > MaxDoings)
            {
                var old = doings.FirstOrDefault(x => x.kind != "site") ?? doings[0];
                doings.Remove(old);
            }
            return d;
        }

        public IEnumerable<GodDoing> Of(string kind) => doings.Where(d => d != null && !d.done && d.kind == kind);

        public GodGiftHeld AddGift(GodGiftHeld g)
        {
            if (g == null) return null;
            g.id = nextId++;
            g.since = Now;
            gifts.Add(g);
            while (gifts.Count > MaxGifts) gifts.Remove(gifts.FirstOrDefault(x => !x.Holds) ?? gifts[0]);
            return g;
        }

        public void Bless(int hediffId)
        {
            if (blessed.Contains(hediffId)) return;
            blessed.Add(hediffId);
            while (blessed.Count > MaxBlessed) blessed.RemoveAt(0);
        }

        /// <summary>The day's tidying: what is done or gone let go (a holy place keeps until its home does).</summary>
        public void Prune()
        {
            doings.RemoveAll(d => d == null || d.done || d.Map == null && d.kind != "slight" || d.until >= 0 && Now > d.until + GodWorldMath.DayTicks && d.kind != "site");
            gifts.RemoveAll(g => g == null || g.pawn == null || g.pawn.Discarded);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref gifts, "gifts", LookMode.Deep);
            Scribe_Collections.Look(ref doings, "doings", LookMode.Deep);
            Scribe_Collections.Look(ref blessed, "blessed", LookMode.Value);
            Scribe_Collections.Look(ref lasts, "lasts", LookMode.Value, LookMode.Value);
            Scribe_Values.Look(ref hungerBargains, "hungerBargains");
            Scribe_Values.Look(ref hungerTaken, "hungerTaken");
            Scribe_Values.Look(ref kindness, "kindness");
            Scribe_Values.Look(ref cruelty, "cruelty");
            Scribe_Values.Look(ref nextId, "nextId", 1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                gifts ??= new List<GodGiftHeld>();
                gifts.RemoveAll(g => g == null);
                doings ??= new List<GodDoing>();
                doings.RemoveAll(d => d == null);
                blessed ??= new List<int>();
                lasts ??= new Dictionary<string, int>();
            }
        }
    }
}
