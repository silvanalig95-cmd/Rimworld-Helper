using System.Collections.Generic;
using System.Linq;
using Verse;

namespace ColonyCouncil
{
    /// <summary>One colonist's belief in one god (Gods/Belief/PawnGods.cs): how strongly they hold it and dread it, what their character drew, and why.</summary>
    public class HeldGod : IExposable
    {
        public int god = -1;
        /// <summary>How strongly they hold it, and dread it (0 to 100); what their character drew at first (the floor neglect fades to half of).</summary>
        public float hold, dread, drawn;
        /// <summary>When it last grew, and when the dread last did; -1 never.</summary>
        public int grew = -1, feared = -1;
        /// <summary>The line of why (shown when the belief is hovered): what drew them, or the moment since which they hold it; and the dread's.</summary>
        public string why, dreadWhy;

        public void ExposeData()
        {
            Scribe_Values.Look(ref god, "god", -1);
            Scribe_Values.Look(ref hold, "hold");
            Scribe_Values.Look(ref dread, "dread");
            Scribe_Values.Look(ref drawn, "drawn");
            Scribe_Values.Look(ref grew, "grew", -1);
            Scribe_Values.Look(ref feared, "feared", -1);
            Scribe_Values.Look(ref why, "why");
            Scribe_Values.Look(ref dreadWhy, "dreadWhy");
        }
    }

    /// <summary>A colonist's gods: drawn once when they are first among the colony's people, and when they were won over (a scoffer won over scoffs no more).</summary>
    public class PawnBelief : IExposable
    {
        public Pawn pawn;
        public bool drawn;
        public int wonOver = -1;
        public List<HeldGod> gods = new List<HeldGod>();

        public HeldGod Of(int god) => gods.FirstOrDefault(h => h != null && h.god == god);

        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Values.Look(ref drawn, "drawn");
            Scribe_Values.Look(ref wonOver, "wonOver", -1);
            Scribe_Collections.Look(ref gods, "gods", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                gods ??= new List<HeldGod>();
                gods.RemoveAll(h => h == null || h.god < 0);
            }
        }
    }

    /// <summary>
    /// Each colonist's own belief in the gods, saved with the game (the rules in PawnGodsMath): one record a colonist of the
    /// colony's own, at most PawnGodsMath.MaxHeld gods each; the dead, the departed and anyone no longer the colony's let go
    /// each day.
    /// </summary>
    public class PawnGodsBook : GameComponent
    {
        public List<PawnBelief> people = new List<PawnBelief>();

        [Unsaved] private Dictionary<int, PawnBelief> byPawn;

        private static PawnGodsBook cached;
        private static Game cachedGame;

        public PawnGodsBook(Game game)
        {
        }

        public static PawnGodsBook Instance
        {
            get
            {
                var game = Current.Game;
                if (game == null) return null;
                if (cachedGame != game || cached == null)
                {
                    cached = game.GetComponent<PawnGodsBook>();
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
            byPawn = null;
        }

        /// <summary>Someone's record, or null.</summary>
        public PawnBelief Find(Pawn p)
        {
            if (p == null) return null;
            if (byPawn == null)
            {
                byPawn = new Dictionary<int, PawnBelief>();
                foreach (var r in people)
                    if (r?.pawn != null)
                        byPawn[r.pawn.thingIDNumber] = r;
            }
            return byPawn.TryGetValue(p.thingIDNumber, out var x) ? x : null;
        }

        public PawnBelief Add(Pawn p)
        {
            var r = new PawnBelief { pawn = p };
            people.Add(r);
            byPawn = null;
            return r;
        }

        public void Changed() => byPawn = null;

        public override void ExposeData()
        {
            base.ExposeData();
            // Nobody the game no longer keeps is written.
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                people.RemoveAll(r => r?.pawn == null || r.pawn.Discarded);
                byPawn = null;
            }
            Scribe_Collections.Look(ref people, "people", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                people ??= new List<PawnBelief>();
                people.RemoveAll(r => r?.pawn == null);
                byPawn = null;
            }
        }
    }
}
