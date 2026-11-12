using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>A crossing the river folk laid on the colony's river (item 26): where, of what, and their knot night's rest at it.</summary>
    public class CrossingRecord : IExposable
    {
        public Map map;
        public Faction faction;
        /// <summary>Its cells, water and banks, as laid.</summary>
        public List<IntVec3> cells = new List<IntVec3>();
        /// <summary>The game's bridge it was laid with.</summary>
        public string bridge;
        public int laid = -1;
        /// <summary>The year they last rested at it on their knot night (-1: never).</summary>
        public int knotYear = -1;

        public void ExposeData()
        {
            Scribe_References.Look(ref map, "map");
            Scribe_References.Look(ref faction, "faction");
            Scribe_Collections.Look(ref cells, "cells", LookMode.Value);
            Scribe_Values.Look(ref bridge, "bridge");
            Scribe_Values.Look(ref laid, "laid", -1);
            Scribe_Values.Look(ref knotYear, "knotYear", -1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit) cells ??= new List<IntVec3>();
        }
    }

    /// <summary>A part fitted on the enclave's terms (item 22): what is owed, what is paid, and whether it went to default.</summary>
    public class TermsRecord : IExposable
    {
        public int id;
        public Faction faction;
        public Map map;
        /// <summary>Who the part went into, by name (the colonist may be gone by the last instalment).</summary>
        public string who;
        public string part;
        public int total, down;
        /// <summary>Instalments paid (0 to 4), the next one's due day, and what is still owed.</summary>
        public int paid, due = -1, owed;
        /// <summary>An instalment missed: the debt is the colony's, in the world's ledger (its tie), and no lander comes until it is paid.</summary>
        public bool defaulted;
        public int tie;
        public bool done;

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_References.Look(ref faction, "faction");
            Scribe_References.Look(ref map, "map");
            Scribe_Values.Look(ref who, "who");
            Scribe_Values.Look(ref part, "part");
            Scribe_Values.Look(ref total, "total");
            Scribe_Values.Look(ref down, "down");
            Scribe_Values.Look(ref paid, "paid");
            Scribe_Values.Look(ref due, "due", -1);
            Scribe_Values.Look(ref owed, "owed");
            Scribe_Values.Look(ref defaulted, "defaulted");
            Scribe_Values.Look(ref tie, "tie");
            Scribe_Values.Look(ref done, "done");
        }
    }

    /// <summary>
    /// What the peoples' own events of round 2 keep with the game (Peoples/Events2/): the crossings the river folk laid, the
    /// enclave's terms, the names the guild's marks put into the labels of blades, and how long the league's thanks for its
    /// guard lasts. The rest lives in the events' visits (a season away, a crossing being laid, a fetch, a news line still to
    /// come) and in a camp's own state. A save from before has none of it.
    /// </summary>
    public class PeopleEvents2Book : GameComponent
    {
        public List<CrossingRecord> crossings = new List<CrossingRecord>();
        public List<TermsRecord> terms = new List<TermsRecord>();
        public int nextTerms = 1;
        /// <summary>"thingId:name" of each blade the guild marked with a colonist's name.</summary>
        public List<string> marks = new List<string>();
        /// <summary>Until when the league's caravans come half again as often, for the guard the colony lent (-1: none).</summary>
        public int guardUntil = -1;

        private static PeopleEvents2Book cached;
        private static Game cachedGame;

        public PeopleEvents2Book(Game game)
        {
        }

        public static PeopleEvents2Book Instance
        {
            get
            {
                var game = Current.Game;
                if (game == null) return null;
                if (cachedGame != game || cached == null)
                {
                    cached = game.GetComponent<PeopleEvents2Book>();
                    cachedGame = game;
                    GuildMark.Forget();
                }
                return cached;
            }
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            cached = this;
            cachedGame = Current.Game;
            GuildMark.Forget();
        }

        public void AddCrossing(CrossingRecord r)
        {
            if (r == null) return;
            crossings.Add(r);
            while (crossings.Count > PeopleEvents2Math.MaxCrossings) crossings.RemoveAt(0);
        }

        public TermsRecord AddTerms(TermsRecord r)
        {
            if (r == null) return null;
            r.id = nextTerms++;
            terms.Add(r);
            while (terms.Count > PeopleEvents2Math.MaxTerms && terms.Any(t => t.done)) terms.Remove(terms.First(t => t.done));
            return r;
        }

        public void AddMark(int thingId, string name)
        {
            string key = thingId + ":";
            marks.RemoveAll(m => m.StartsWith(key));
            marks.Add(key + name);
            while (marks.Count > PeopleEvents2Math.MaxMarks) marks.RemoveAt(0);
            GuildMark.Forget();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref crossings, "crossings", LookMode.Deep);
            Scribe_Collections.Look(ref terms, "terms", LookMode.Deep);
            Scribe_Values.Look(ref nextTerms, "nextTerms", 1);
            Scribe_Collections.Look(ref marks, "marks", LookMode.Value);
            Scribe_Values.Look(ref guardUntil, "guardUntil", -1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                crossings ??= new List<CrossingRecord>();
                crossings.RemoveAll(c => c == null);
                terms ??= new List<TermsRecord>();
                terms.RemoveAll(t => t == null);
                marks ??= new List<string>();
                GuildMark.Forget();
            }
        }
    }
}
