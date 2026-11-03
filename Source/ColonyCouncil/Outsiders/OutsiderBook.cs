using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// What passed between one colonist and one person from outside the colony: when they last did each thing together,
    /// the bond between them (it grows over visits and fades between them), how well they know each other, how many
    /// visits they met on, and whether they became friends or sweethearts.
    /// </summary>
    public class OutsiderPair : IExposable
    {
        public Pawn colonist;
        public Pawn outsider;
        public string colonistName;
        public string outsiderName;
        public Faction faction;
        /// <summary>The bond between them, -100 to 100.</summary>
        public float warmth;
        /// <summary>How well they know each other, 0 to 100.</summary>
        public float fam;
        public int visits;
        /// <summary>When they last did something together, and when the bond last faded.</summary>
        public int lastTick = -1;
        public int faded = -1;
        public bool friends;
        /// <summary>0 nothing, 1 sweethearts, 2 lovers; and the visits counted when they became sweethearts.</summary>
        public int court;
        public int courtVisits;
        /// <summary>The bond went to their regard as a notable once they were known by name.</summary>
        public bool carried;
        /// <summary>When the two last did each thing (by <see cref="OutsiderAct"/>), -1 never.</summary>
        public List<int> last = new List<int>();

        public int Last(OutsiderAct a)
        {
            int i = (int)a;
            return i < last.Count ? last[i] : -1;
        }

        public void SetLast(OutsiderAct a, int tick)
        {
            int i = (int)a;
            while (last.Count <= i) last.Add(-1);
            last[i] = tick;
        }

        public void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                if (colonist != null && colonist.Discarded) colonist = null;
                if (outsider != null && outsider.Discarded) outsider = null;
            }
            Scribe_References.Look(ref colonist, "colonist");
            Scribe_References.Look(ref outsider, "outsider");
            Scribe_Values.Look(ref colonistName, "colonistName");
            Scribe_Values.Look(ref outsiderName, "outsiderName");
            Scribe_References.Look(ref faction, "faction");
            Scribe_Values.Look(ref warmth, "warmth");
            Scribe_Values.Look(ref fam, "fam");
            Scribe_Values.Look(ref visits, "visits");
            Scribe_Values.Look(ref lastTick, "lastTick", -1);
            Scribe_Values.Look(ref faded, "faded", -1);
            Scribe_Values.Look(ref friends, "friends");
            Scribe_Values.Look(ref court, "court");
            Scribe_Values.Look(ref courtVisits, "courtVisits");
            Scribe_Values.Look(ref carried, "carried");
            Scribe_Collections.Look(ref last, "last", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit) last ??= new List<int>();
        }
    }

    /// <summary>Something asked of an outsider for their people's next visit: what, how many, who asked.</summary>
    public class OutsiderFavour : IExposable
    {
        public Pawn outsider;
        public string outsiderName;
        public Faction faction;
        public Pawn by;
        public string byName;
        public ThingDef def;
        public int count;
        public int asked;
        /// <summary>Someone of theirs is on the way over with it now (not saved: after a load they come round again).</summary>
        [Unsaved] public Pawn bearer;

        public void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                if (outsider != null && outsider.Discarded) outsider = null;
                if (by != null && by.Discarded) by = null;
            }
            Scribe_References.Look(ref outsider, "outsider");
            Scribe_Values.Look(ref outsiderName, "outsiderName");
            Scribe_References.Look(ref faction, "faction");
            Scribe_References.Look(ref by, "by");
            Scribe_Values.Look(ref byName, "byName");
            Scribe_Defs.Look(ref def, "def");
            Scribe_Values.Look(ref count, "count");
            Scribe_Values.Look(ref asked, "asked");
        }
    }

    /// <summary>A good word an outsider promised to put in at home, with one of their notables, once they're back.</summary>
    public class OutsiderWord : IExposable
    {
        public Pawn outsider;
        public string outsiderName;
        public int notable;
        public Pawn by;
        public string byName;
        public float shift;
        public int asked;

        public void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                if (outsider != null && outsider.Discarded) outsider = null;
                if (by != null && by.Discarded) by = null;
            }
            Scribe_References.Look(ref outsider, "outsider");
            Scribe_Values.Look(ref outsiderName, "outsiderName");
            Scribe_Values.Look(ref notable, "notable");
            Scribe_References.Look(ref by, "by");
            Scribe_Values.Look(ref byName, "byName");
            Scribe_Values.Look(ref shift, "shift");
            Scribe_Values.Look(ref asked, "asked");
        }
    }

    /// <summary>
    /// The record of what the colony's people did with people from outside it (Outsiders/Outsiders.cs): one line a pair,
    /// the favours asked for their people's next visit, and the good words promised. Its own game component; the hour's
    /// upkeep runs from the council's people hour, and the 250-tick look (meals, bouts and brawls under way) at
    /// <see cref="Sched.RareOutsiders"/>, only while one is.
    /// </summary>
    public class OutsiderBook : GameComponent
    {
        /// <summary>Pairs kept at most: the longest quiet go first.</summary>
        public const int MaxPairs = 60;
        public const int MaxFavours = 6;
        public const int MaxWords = 8;

        public List<OutsiderPair> pairs = new List<OutsiderPair>();
        public List<OutsiderFavour> favours = new List<OutsiderFavour>();
        public List<OutsiderWord> words = new List<OutsiderWord>();

        private static OutsiderBook cached;
        private static Game cachedGame;

        public OutsiderBook(Game game)
        {
        }

        public static OutsiderBook Instance
        {
            get
            {
                var game = Current.Game;
                if (game == null) return null;
                if (cachedGame != game || cached == null)
                {
                    cached = game.GetComponent<OutsiderBook>();
                    cachedGame = game;
                }
                return cached;
            }
        }

        private static int Now => Find.TickManager.TicksGame;

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            cached = this;
            cachedGame = Current.Game;
            Outsiders.Reset();
        }

        public override void GameComponentTick()
        {
            if (!Outsiders.AnyLive) return;
            int now = Now;
            if (!Sched.Every(now, Sched.RareTicks, Sched.RareOutsiders)) return;
            try
            {
                Outsiders.Rare(now);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("outsiders rare", e);
            }
        }

        /// <summary>The pair's record; with <paramref name="make"/>, a new one if there is none.</summary>
        public OutsiderPair Pair(Pawn c, Pawn x, bool make)
        {
            if (c == null || x == null || c == x) return null;
            var p = pairs.FirstOrDefault(r => r.colonist == c && r.outsider == x);
            if (p != null || !make) return p;
            p = new OutsiderPair { colonist = c, outsider = x, colonistName = c.LabelShort, outsiderName = x.LabelShort, faction = Outsiders.PeopleOf(x), faded = Now };
            pairs.Add(p);
            while (pairs.Count > MaxPairs)
                pairs.Remove(pairs.Where(r => r != p).OrderBy(r => r.colonist == null || r.outsider == null || r.outsider.Dead ? 0 : 1).ThenBy(r => r.lastTick).First());
            return p;
        }

        /// <summary>When anyone of the colony last did this with them, -1 never.</summary>
        public int LastWith(Pawn x, OutsiderAct a)
        {
            int best = -1;
            foreach (var r in pairs)
                if (r.outsider == x)
                    best = Math.Max(best, r.Last(a));
            return best;
        }

        /// <summary>Anyone of the colony is friends with them (a reason to stay).</summary>
        public bool AnyFriend(Pawn x) => pairs.Any(r => r.outsider == x && r.friends && KinMemory.Ours(r.colonist));

        // ---------------- The hour ----------------

        public void Hourly()
        {
            int now = Now;
            Safe("words", () => Outsiders.WordsHourly(this, now));
            Safe("favours", () => Outsiders.FavoursHourly(this, now));
            Safe("pairs", () => Upkeep(now));
        }

        private static void Safe(string what, Action a)
        {
            try
            {
                a();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("outsiders " + what, e);
            }
        }

        /// <summary>The bonds fade a little, the known faces take theirs along, the gone are let go.</summary>
        private void Upkeep(int now)
        {
            foreach (var r in pairs)
            {
                float days = (now - (r.faded < 0 ? now : r.faded)) / (float)GenDate.TicksPerDay;
                if (days >= 1f)
                {
                    r.warmth = OutsiderMath.Fade(r.warmth, days);
                    r.faded = now;
                }
                if (r.outsider != null && !r.outsider.Discarded) r.outsiderName = r.outsider.LabelShort;
                // Known by name now (a notable, or a face met who went home): the bond counts toward their regard of the colonist.
                if (!r.carried && Math.Abs(r.warmth) >= 1f && KinMemory.Ours(r.colonist))
                {
                    var n = Outsiders.NotableOf(r.outsider);
                    if (n != null)
                    {
                        n.RegardOf(r.colonist, true).personal += r.warmth * 0.5f;
                        r.carried = true;
                        NotableBook.Changed();
                    }
                }
            }
            pairs.RemoveAll(r => r.colonist == null || r.outsider == null || r.outsider.Discarded
                                 || (r.outsider.Dead || !KinMemory.Ours(r.colonist)) && now - r.lastTick > GenDate.TicksPerDay * 30);
        }

        // ---------------- Saving ----------------

        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                pairs.RemoveAll(r => r == null || r.colonist == null && r.outsider == null);
                favours.RemoveAll(f => f == null || f.def == null || f.faction == null);
                words.RemoveAll(w => w == null);
            }
            Scribe_Collections.Look(ref pairs, "pairs", LookMode.Deep);
            Scribe_Collections.Look(ref favours, "favours", LookMode.Deep);
            Scribe_Collections.Look(ref words, "words", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                pairs ??= new List<OutsiderPair>();
                pairs.RemoveAll(r => r == null || r.colonist == null || r.outsider == null);
                favours ??= new List<OutsiderFavour>();
                favours.RemoveAll(f => f == null || f.def == null || f.faction == null);
                words ??= new List<OutsiderWord>();
                words.RemoveAll(w => w == null);
            }
        }
    }
}
