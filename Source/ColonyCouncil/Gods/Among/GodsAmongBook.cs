using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>A named season a believer took for a god's doing (Gods/Among/SeasonGods.cs): which, when, whose, and whether its day was asked.</summary>
    public class SeasonTaken : IExposable
    {
        public Map home;
        public int year, season;
        /// <summary>WeatherQuarter.name ("cold", "heat", "storms", "wet", "dry") and the name it is remembered by.</summary>
        public string kind, text;
        public int god = -1;
        public string believer;
        public bool asked;

        public void ExposeData()
        {
            Scribe_References.Look(ref home, "home");
            Scribe_Values.Look(ref year, "year");
            Scribe_Values.Look(ref season, "season");
            Scribe_Values.Look(ref kind, "kind");
            Scribe_Values.Look(ref text, "text");
            Scribe_Values.Look(ref god, "god", -1);
            Scribe_Values.Look(ref believer, "believer");
            Scribe_Values.Look(ref asked, "asked");
        }
    }

    /// <summary>
    /// The gods among the colony's people and the world (the content update's integrations, part 2; the player's go of 20
    /// November 2026), saved with the game: Ducat's turns after a debt (the coin on the doorstep, the trader who stays, the
    /// hard bargain), the keeper's walk to a sign and the evening's writing, the named seasons taken for a god's doing, and
    /// the midwinter evening under way. The parts are in Gods/Among/; the pure rules in GodsAmongMath. Its hour and day run
    /// with the gods' own (GodBook.Hourly, GodBook.Daily); its steps between them only while something is under way.
    /// </summary>
    public class GodsAmongBook : GameComponent
    {
        // ---- A vowed pilgrimage that really walks out (VowPilgrimage.cs) ----
        /// <summary>Pilgrims home with a tale owed at supper.</summary>
        public List<PilgrimTale> tales = new List<PilgrimTale>();

        public const int MaxTales = 6;

        // ---- The dead's own god at the grave (GraveGods.cs) ----
        /// <summary>The god each of the colony who died lately held, kept for their eulogy.</summary>
        public List<DeadHeld> deadHeld = new List<DeadHeld>();

        public const int MaxDeadHeld = 12;

        // ---- Ducat and what the colony owes (DucatDebts.cs) ----
        public Pawn coinFor;
        public Map coinMap;
        /// <summary>The coin lies on the doorstep the morning after this tick.</summary>
        public int coinAfter = -1;
        /// <summary>The next trader at the gate stays longer: at this home, waiting until then.</summary>
        public Map longerMap;
        public int longerUntil = -1;
        /// <summary>The next trader drives a hard bargain: at this home, waiting until then; then the trader who does, until then.</summary>
        public Map hardMap;
        public int hardUntil = -1;
        public Pawn hardTrader;
        public int hardTraderUntil = -1;

        // ---- The keeper at work (KeeperAtWork.cs) ----
        /// <summary>The newest entry of the colony's book the keeper has written up (its tick).</summary>
        public int keeperWrote = -1;
        /// <summary>The keeper walking out to a sign they didn't see: who, where, until when, and the entry (its tick and page).</summary>
        public Pawn looker;
        public Map lookMap;
        public IntVec3 lookAt = IntVec3.Invalid;
        public int lookUntil = -1, lookEntry = -1;
        public string lookPage;
        /// <summary>The keeper sitting at the evening's writing, until then.</summary>
        public Pawn writer;
        public int writeUntil = -1;

        // ---- Named seasons taken for a god's doing (SeasonGods.cs) ----
        public List<SeasonTaken> seasons = new List<SeasonTaken>();

        // ---- Midwinter (Midwinter.cs) ----
        public Map mwMap;
        /// <summary>The home's day it is kept (its year and day of the year), and the stages done (GodsAmongMath.StageAt), as bits.</summary>
        public int mwDay = -1, mwDone;
        /// <summary>Who hands round the presents, how many, and who has had theirs (thing ids).</summary>
        public Pawn mwGiver;
        public string mwGiven;
        /// <summary>The sculptor in the snow, the cell, and until when.</summary>
        public Pawn mwSculptor;
        public IntVec3 mwSnowAt = IntVec3.Invalid;
        public int mwSnowUntil = -1;

        public GodsAmongBook(Game game)
        {
        }

        private static GodsAmongBook cached;
        private static Game cachedGame;

        public static GodsAmongBook Instance
        {
            get
            {
                var game = Current.Game;
                if (game == null) return null;
                if (cachedGame != game || cached == null)
                {
                    cached = game.GetComponent<GodsAmongBook>();
                    cachedGame = game;
                }
                return cached;
            }
        }

        /// <summary>The book while the gods are on.</summary>
        public static GodsAmongBook Book => Gods.On ? Instance : null;

        internal static int Now => Find.TickManager?.TicksGame ?? 0;

        [Unsaved] private int nextDue = int.MaxValue;

        /// <summary>Something under way has a step due at <paramref name="tick"/> (the earliest is kept).</summary>
        public void Due(int tick)
        {
            if (tick < nextDue) nextDue = tick;
        }

        public override void GameComponentTick()
        {
            if (nextDue == int.MaxValue) return;
            int now = Find.TickManager.TicksGame;
            if (now < nextDue) return;
            nextDue = int.MaxValue;
            if (!Gods.On) return;
            Safe("the keeper's steps", () => KeeperAtWork.Step(this, now));
            Safe("Ducat's steps", () => DucatDebts.Step(this, now));
            Safe("the raiders' cries", () => RaidersGods.Step(this, now));
            Safe("midwinter's steps", () => Midwinter.Step(this, now));
        }

        /// <summary>The gods' hour (GodBook.Hourly).</summary>
        public static void Hourly(int now)
        {
            var b = Book;
            if (b == null) return;
            Safe("a vowed pilgrim's tale", () => VowPilgrimage.Hourly(now));
            Safe("Ducat's hour", () => DucatDebts.Hourly(b, now));
            Safe("the keeper's hour", () => KeeperAtWork.Hourly(b, now));
            Safe("the raiders' cries", () => RaidersGods.Hourly(b, now));
            Safe("midwinter's hour", () => Midwinter.Hourly(b, now));
            b.Due(now + 1);
        }

        /// <summary>The gods' day (GodBook.Daily).</summary>
        public static void Daily(int now)
        {
            var b = Book;
            if (b == null) return;
            Safe("the named seasons", () => SeasonGods.Daily(b, now));
            b.Prune(now);
        }

        /// <summary>The day's pruning: gone people let go, old turns and seasons dropped.</summary>
        public void Prune(int now)
        {
            if (coinFor != null && (coinFor.Dead || coinFor.Discarded)) ClearCoin();
            if (longerUntil >= 0 && (now > longerUntil || longerMap == null)) ClearLonger();
            if (hardUntil >= 0 && (now > hardUntil || hardMap == null)) ClearHard();
            if (hardTrader != null && (now > hardTraderUntil || hardTrader.Discarded)) hardTrader = null;
            if (looker != null && (looker.Dead || looker.Discarded || now > lookUntil)) ClearLook();
            if (writer != null && (writer.Dead || writer.Discarded || now > writeUntil)) writer = null;
            tales.RemoveAll(t => t == null || t.pawn == null || t.pawn.Dead || t.pawn.Discarded || now > t.until);
            while (tales.Count > MaxTales) tales.RemoveAt(0);
            deadHeld.RemoveAll(d => d == null || d.pawn == null || d.pawn.Discarded || now - d.tick > 5 * GenDate.TicksPerDay);
            seasons.RemoveAll(s => s == null || s.home == null || s.text.NullOrEmpty());
            while (seasons.Count > GodsAmongMath.MaxSeasons) seasons.RemoveAt(0);
        }

        public void ClearCoin()
        {
            coinFor = null;
            coinMap = null;
            coinAfter = -1;
        }

        public void ClearLonger()
        {
            longerMap = null;
            longerUntil = -1;
        }

        public void ClearHard()
        {
            hardMap = null;
            hardUntil = -1;
        }

        public void ClearLook()
        {
            looker = null;
            lookMap = null;
            lookAt = IntVec3.Invalid;
            lookUntil = lookEntry = -1;
            lookPage = null;
        }

        internal static void Safe(string what, Action a)
        {
            try
            {
                a();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods among the people: " + what, e);
            }
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            cached = this;
            cachedGame = Current.Game;
            nextDue = Find.TickManager.TicksGame + 1;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref tales, "tales", LookMode.Deep);
            Scribe_Collections.Look(ref deadHeld, "deadHeld", LookMode.Deep);
            Scribe_References.Look(ref coinFor, "coinFor");
            Scribe_References.Look(ref coinMap, "coinMap");
            Scribe_Values.Look(ref coinAfter, "coinAfter", -1);
            Scribe_References.Look(ref longerMap, "longerMap");
            Scribe_Values.Look(ref longerUntil, "longerUntil", -1);
            Scribe_References.Look(ref hardMap, "hardMap");
            Scribe_Values.Look(ref hardUntil, "hardUntil", -1);
            Scribe_References.Look(ref hardTrader, "hardTrader");
            Scribe_Values.Look(ref hardTraderUntil, "hardTraderUntil", -1);
            Scribe_Values.Look(ref keeperWrote, "keeperWrote", -1);
            Scribe_References.Look(ref looker, "looker");
            Scribe_References.Look(ref lookMap, "lookMap");
            Scribe_Values.Look(ref lookAt, "lookAt", IntVec3.Invalid);
            Scribe_Values.Look(ref lookUntil, "lookUntil", -1);
            Scribe_Values.Look(ref lookEntry, "lookEntry", -1);
            Scribe_Values.Look(ref lookPage, "lookPage");
            Scribe_References.Look(ref writer, "writer");
            Scribe_Values.Look(ref writeUntil, "writeUntil", -1);
            Scribe_Collections.Look(ref seasons, "seasons", LookMode.Deep);
            Scribe_References.Look(ref mwMap, "mwMap");
            Scribe_Values.Look(ref mwDay, "mwDay", -1);
            Scribe_Values.Look(ref mwDone, "mwDone");
            Scribe_References.Look(ref mwGiver, "mwGiver");
            Scribe_Values.Look(ref mwGiven, "mwGiven");
            Scribe_References.Look(ref mwSculptor, "mwSculptor");
            Scribe_Values.Look(ref mwSnowAt, "mwSnowAt", IntVec3.Invalid);
            Scribe_Values.Look(ref mwSnowUntil, "mwSnowUntil", -1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                tales ??= new List<PilgrimTale>();
                tales.RemoveAll(t => t?.pawn == null);
                deadHeld ??= new List<DeadHeld>();
                deadHeld.RemoveAll(d => d?.pawn == null);
                seasons ??= new List<SeasonTaken>();
                seasons.RemoveAll(s => s == null || s.home == null);
                if (coinMap == null) ClearCoin();
                if (lookMap == null) ClearLook();
                if (mwMap == null) mwDay = -1;
            }
        }
    }
}
