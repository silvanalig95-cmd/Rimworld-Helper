using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The one old power's happening under way at a time (OldPowersBook.play): which, where, its stage, who and what it is
    /// about, and when its next step is due. Let go when it is over, so nothing of it stays in the save.
    /// </summary>
    public class OldPowerPlay : IExposable
    {
        public OldPower kind;
        public Map map;
        public int stage;
        /// <summary>When it ends at the latest, whatever stage it is at.</summary>
        public int until = -1;
        /// <summary>Its next short step (the frame's tick).</summary>
        public int next = -1;
        /// <summary>The visitor, the beast or the one it is about (a), and the one of the colony who goes to it (b).</summary>
        public Pawn a, b;
        /// <summary>The pilgrims walking together, at most five.</summary>
        public List<Pawn> group = new List<Pawn>();
        public Faction faction;
        /// <summary>The stone, the piece to be worked, the grave or the body, the find.</summary>
        public Thing thing;
        public IntVec3 cell = IntVec3.Invalid, cell2 = IntVec3.Invalid;
        public int god = -1;
        public int count, value;
        /// <summary>The form it took (the age's, the find's, the word's), and a word kept for the card.</summary>
        public string form, note;
        public bool strong;

        public void ExposeData()
        {
            Scribe_Values.Look(ref kind, "kind");
            Scribe_References.Look(ref map, "map");
            Scribe_Values.Look(ref stage, "stage");
            Scribe_Values.Look(ref until, "until", -1);
            Scribe_Values.Look(ref next, "next", -1);
            Scribe_References.Look(ref a, "a");
            Scribe_References.Look(ref b, "b");
            Scribe_Collections.Look(ref group, "group", LookMode.Reference);
            Scribe_References.Look(ref faction, "faction");
            Scribe_References.Look(ref thing, "thing");
            Scribe_Values.Look(ref cell, "cell", IntVec3.Invalid);
            Scribe_Values.Look(ref cell2, "cell2", IntVec3.Invalid);
            Scribe_Values.Look(ref god, "god", -1);
            Scribe_Values.Look(ref count, "count");
            Scribe_Values.Look(ref value, "value");
            Scribe_Values.Look(ref form, "form");
            Scribe_Values.Look(ref note, "note");
            Scribe_Values.Look(ref strong, "strong");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                group ??= new List<Pawn>();
                group.RemoveAll(p => p == null);
            }
        }
    }

    /// <summary>
    /// The old powers' happenings as they stand, saved with the game: when each last came (one a happening), when the last
    /// of any came, the short memory of the forms they took, the rooms of the old ruins already searched for a scroll (a few,
    /// the oldest let go), and the one under way. Its short steps run on their own due tick, and nothing at all with none
    /// under way; the looks for a new one ride on the world events' hour (HappeningTracker.Hourly).
    /// </summary>
    public class OldPowersBook : GameComponent
    {
        /// <summary>When each happening last came (OldPowersMath.All's order), or long ago.</summary>
        public List<int> last = new List<int>();
        public int lastAny = -999999;
        /// <summary>The forms lately ("seer|1|"), GodPaceMath.Memory at most.</summary>
        public List<string> lately = new List<string>();
        /// <summary>Rooms of the old ruins searched for a scroll (OwnUndertow.RuinKey), OldPowersMath.RuinsKept at most.</summary>
        public List<string> searched = new List<string>();
        public OldPowerPlay play;

        public OldPowersBook(Game game)
        {
        }

        private static OldPowersBook cached;
        private static Game cachedGame;

        public static OldPowersBook Instance
        {
            get
            {
                var game = Current.Game;
                if (game == null) return null;
                if (cachedGame != game || cached == null)
                {
                    cached = game.GetComponent<OldPowersBook>();
                    cachedGame = game;
                }
                return cached;
            }
        }

        [Unsaved] private int nextDue = int.MaxValue;

        /// <summary>Something under way has a step due at <paramref name="tick"/> (the earliest is kept).</summary>
        public void Due(int tick)
        {
            if (tick < nextDue) nextDue = tick;
        }

        public override void GameComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            if (now < nextDue) return;
            nextDue = int.MaxValue;
            OldPowers.Step(this, now);
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            cached = this;
            cachedGame = Current.Game;
            Fit();
            nextDue = Find.TickManager.TicksGame + 1;
        }

        /// <summary>One entry a happening, long ago for one new since the save.</summary>
        public void Fit()
        {
            last ??= new List<int>();
            while (last.Count < OldPowersMath.All.Length) last.Add(-999999);
            if (last.Count > OldPowersMath.All.Length) last.RemoveRange(OldPowersMath.All.Length, last.Count - OldPowersMath.All.Length);
            lately ??= new List<string>();
            searched ??= new List<string>();
        }

        public int Last(OldPower e)
        {
            Fit();
            return last[(int)e];
        }

        /// <summary>It came: its tick and the last of any kept, and the form it took remembered.</summary>
        public void Came(OldPower e, int now, string form = null, string who = null)
        {
            Fit();
            last[(int)e] = now;
            lastAny = now;
            GodPaceMath.Note(lately, OldPowersMath.Name(e), form ?? "", who);
        }

        /// <summary>The forms it took lately, newest first.</summary>
        public List<string> Forms(OldPower e) => GodPaceMath.Forms(lately, OldPowersMath.Name(e));

        /// <summary>A room of the old ruins searched for a scroll, remembered (the oldest let go).</summary>
        public void Searched(string key)
        {
            if (key.NullOrEmpty() || searched.Contains(key)) return;
            searched.Add(key);
            while (searched.Count > OldPowersMath.RuinsKept) searched.RemoveAt(0);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref last, "last", LookMode.Value);
            Scribe_Values.Look(ref lastAny, "lastAny", -999999);
            Scribe_Collections.Look(ref lately, "lately", LookMode.Value);
            Scribe_Collections.Look(ref searched, "searched", LookMode.Value);
            Scribe_Deep.Look(ref play, "play");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                Fit();
                if (play != null && play.map == null) play = null;
                nextDue = 0;
            }
        }
    }

    /// <summary>
    /// The old powers' question (the wandering enchanter's offer: OldPowersVisitors): let them work the piece, or send them
    /// on their way. Answered only while the enchanter still waits; left unanswered, they go on their way.
    /// </summary>
    public class ChoiceLetter_OldPowers : RimfolkLetter
    {
        public bool answered;

        private bool Live => !answered && !ArchivedOnly && OldPowersVisitors.EnchanterWaiting;

        public override IEnumerable<DiaOption> Choices
        {
            get
            {
                if (!Live)
                {
                    yield return Option_Close;
                    yield break;
                }
                var yes = new DiaOption(OldPowersVisitors.YesLabel())
                {
                    action = () =>
                    {
                        answered = true;
                        Find.LetterStack.RemoveLetter(this);
                        OldPowersVisitors.Answer(true);
                    },
                    resolveTree = true
                };
                string why = OldPowersVisitors.WhyNot();
                if (why != null) yes.Disable(why);
                yield return Answers.Recorded(yes, this, "oldPowers", OldPowersBook.Instance?.play?.faction);
                yield return Answers.Recorded(new DiaOption("CC_OldP_Enchanter_No".Translate())
                {
                    action = () =>
                    {
                        answered = true;
                        Find.LetterStack.RemoveLetter(this);
                        OldPowersVisitors.Answer(false);
                    },
                    resolveTree = true
                }, this, "oldPowers", OldPowersBook.Instance?.play?.faction);
                yield return Option_Postpone;
            }
        }

        public override void Removed()
        {
            base.Removed();
            if (answered || ArchivedOnly) return;
            answered = true;
            if (OldPowersVisitors.EnchanterWaiting) OldPowersVisitors.Answer(false);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref answered, "answered");
        }
    }
}
