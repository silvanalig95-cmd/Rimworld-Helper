using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>A colonist's last ask for a better piece of clothing: when, which piece (its id), its words for the inbox.</summary>
    public class ApparelAskNote : IExposable
    {
        public int pawn = -1, tick = -1, piece = -1;
        public string label, place;
        public float gain;
        /// <summary>Asked for warmth (the inbox says it was for the cold).</summary>
        public bool warmth;

        public void ExposeData()
        {
            Scribe_Values.Look(ref pawn, "pawn", -1);
            Scribe_Values.Look(ref tick, "tick", -1);
            Scribe_Values.Look(ref piece, "piece", -1);
            Scribe_Values.Look(ref label, "label");
            Scribe_Values.Look(ref place, "place");
            Scribe_Values.Look(ref gain, "gain");
            Scribe_Values.Look(ref warmth, "warmth");
        }
    }

    /// <summary>
    /// What the clothes suggestions keep with the game (docs/design/apparel.md, step 3): the colonists whose suggestions the
    /// player set aside ("not now", for <see cref="ApparelAskMath.NotNowDays"/> days), each colonist's last ask (one each) and
    /// the colony's last ask. The raids it reads are the weapons' record (WeaponPickBook). Its clock: every 250 ticks at
    /// <see cref="Sched.RareApparelPicks"/>, a tenth of the colonists each time (each once an hour): their ratings of the
    /// colony's clothes worked out ahead, then their look for a piece to ask for. A save from before has an empty one.
    /// </summary>
    public class ApparelPickBook : GameComponent
    {
        /// <summary>Colonists' suggestions set aside (thingIDNumber: until when).</summary>
        public Dictionary<int, int> notNow = new Dictionary<int, int>();
        public List<ApparelAskNote> asks = new List<ApparelAskNote>();
        public int lastAsk = -1;

        private static ApparelPickBook cached;
        private static Game cachedGame;

        public ApparelPickBook(Game game)
        {
        }

        public static ApparelPickBook Instance
        {
            get
            {
                var game = Current.Game;
                if (game == null) return null;
                if (cachedGame != game || cached == null)
                {
                    cached = game.GetComponent<ApparelPickBook>();
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
            ApparelPick.Reset();
        }

        private static int Now => Find.TickManager?.TicksGame ?? 0;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref notNow, "notNow", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref asks, "asks", LookMode.Deep);
            Scribe_Values.Look(ref lastAsk, "lastAsk", -1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                notNow ??= new Dictionary<int, int>();
                asks ??= new List<ApparelAskNote>();
                asks.RemoveAll(a => a == null || a.pawn < 0);
            }
        }

        // ---------------- Not now ----------------

        public int NotNowUntil(Pawn p) => p != null && notNow.TryGetValue(p.thingIDNumber, out var until) ? until : 0;

        public bool IsNotNow(Pawn p) => NotNowUntil(p) > Now;

        /// <summary>The player sets a colonist's clothes suggestions aside for a while (they don't ask either), or brings them back.</summary>
        public void SetNotNow(Pawn p, bool aside)
        {
            if (p == null) return;
            if (aside) notNow[p.thingIDNumber] = Now + ApparelAskMath.NotNowDays * GenDate.TicksPerDay;
            else notNow.Remove(p.thingIDNumber);
            ApparelPick.Dirty();
        }

        // ---------------- Asks ----------------

        public ApparelAskNote AskOf(Pawn p) => p == null ? null : asks.FirstOrDefault(a => a.pawn == p.thingIDNumber);

        public void Asked(Pawn p, Thing piece, string place, float gain, bool warmth)
        {
            var a = AskOf(p);
            if (a == null) asks.Add(a = new ApparelAskNote { pawn = p.thingIDNumber });
            a.tick = Now;
            a.piece = piece.thingIDNumber;
            a.label = piece.LabelNoCount;
            a.place = place;
            a.gain = gain;
            a.warmth = warmth;
            lastAsk = Now;
        }

        // ---------------- The clock ----------------

        public override void GameComponentTick()
        {
            var tm = Find.TickManager;
            if (tm == null) return;
            int now = tm.TicksGame;
            if (!Sched.Every(now, Sched.RareTicks, Sched.RareApparelPicks)) return;
            if (!ApparelPick.On && !ApparelAsks.On) return;
            int slice = Sched.SliceTurn(now, Sched.HourTicks, Sched.Slices, Sched.RareApparelPicks);
            if (slice < 0) return;
            using (PassClock.Time(Pass.RareApparelPicks))
            {
                try
                {
                    foreach (var map in Homes.All)
                    {
                        var colonists = ApparelPick.Colonists(map).Where(p => Sched.SliceOf(p.thingIDNumber, Sched.Slices) == slice).ToList();
                        if (colonists.Count == 0) continue;
                        var picks = ApparelPick.Current(map);
                        foreach (var p in colonists) ApparelPick.Warm(picks, p);
                        if (!ApparelAsks.On) continue;
                        picks = ApparelPick.For(map);
                        foreach (var p in colonists) ApparelAsks.Look(p, picks, now);
                    }
                    if (slice == 0 && Sched.FirstHourOfDay(now)) Prune(now);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("the clothes suggestions' pass", e);
                }
            }
        }

        /// <summary>Once a day: "not now" ended, and asks of colonists no longer here.</summary>
        private void Prune(int now)
        {
            foreach (var k in notNow.Where(kv => kv.Value <= now).Select(kv => kv.Key).ToList()) notNow.Remove(k);
            var here = new HashSet<int>(Homes.FreeColonistsAnywhere().Select(p => p.thingIDNumber));
            asks.RemoveAll(a => !here.Contains(a.pawn) && now - a.tick > ApparelAskMath.SamePieceDays * GenDate.TicksPerDay);
        }
    }
}
